using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Diagnostics;
using fNbt;
using System.Globalization;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Minecraft_Version_History
{
    public static class CommandRunner
    {
        public static CommandResult RunCommand(string cd, string input, bool output = false, bool suppress_errors = false)
        {
#if DEBUG
            Console.WriteLine($"Running this command: {input}");
#endif
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = cd;
            cmd.StartInfo.Arguments = $"/S /C \"{input}\"";
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.RedirectStandardError = suppress_errors;
            cmd.StartInfo.RedirectStandardOutput = output;
            cmd.Start();
            string result = null;
            if (output)
                result = cmd.StandardOutput.ReadToEnd();
            cmd.WaitForExit();
            return new CommandResult { ExitCode = cmd.ExitCode, Output = result };
        }

        public struct CommandResult
        {
            public string Output;
            public int ExitCode;
        }
    }

    public abstract class Updater<T> where T : Version
    {
        private static Regex ReleaseRegex = new Regex(@"(.*?)1\.(\d*)");
        protected string RepoFolder { get; private set; }
        protected string VersionsFolder { get; private set; }
        // key = version, value = commit hash
        protected Dictionary<T, string> CommittedVersionDict;
        protected List<T> UncommittedVersionList;
        public IEnumerable<T> CommittedVersions => CommittedVersionDict.Keys;
        public IEnumerable<T> UncommitedVersions => UncommittedVersionList;
        readonly static string[] GitFolders = new[] { ".git" };
        readonly static string[] GitFiles = new[] { ".gitignore", "version.txt" };
        public Updater(string repo_folder, string versions_folder)
        {
            RepoFolder = repo_folder;
            VersionsFolder = versions_folder;
            CommittedVersionDict = new Dictionary<T, string>();
            UncommittedVersionList = new List<T>();
            Console.WriteLine("Scanning versions...");
            string all = CommandRunner.RunCommand(RepoFolder, $"git log --all", output: true).Output;
            foreach (var version in GetAllVersions())
            {
                int index = all.IndexOf($"\n\n    {version.VersionName}\n");
                if (index == -1)
                {
                    UncommittedVersionList.Add(version);
                    Console.WriteLine($"New version: {version.VersionName}");
                }
                else
                {
                    int hash_index = all.LastIndexOf("commit ", index);
                    string hash = all.Substring(hash_index + "commit ".Length, 40);
                    CommittedVersionDict.Add(version, hash);
                }
            }
        }

        protected virtual T SpecialParent(T version, List<T> history)
        {
            return null;
        }

        protected virtual bool NoParentsAllowed(T version)
        {
            return false;
        }

        private T Parent(T version, List<T> history)
        {
            var special = SpecialParent(version, history);
            if (special != null)
                return special;
            T parent;
            int my_index = history.IndexOf(version);
            parent = history.LastOrDefault(x => version != x && version.ReleaseName == x.ReleaseName && version.ReleaseTime >= x.ReleaseTime && history.IndexOf(x) < my_index);
            if (parent == null && my_index > 0)
            {
                int search = my_index;
                while (true)
                {
                    search--;
                    if (NoParentsAllowed(history[search]))
                        continue;
                    return history[search];
                }
            }
            return parent;
        }

        public void CommitChanges()
        {
            var idealhistory = CommittedVersions.Concat(UncommitedVersions).OrderBy(x => x, VersionComparer.Instance).ToList();
            Console.WriteLine("Ideal history:");
            foreach (var version in idealhistory)
            {
                Console.WriteLine($"{version} (comes after {Parent(version, idealhistory)})");
            }
            foreach (var version in idealhistory)
            {
                CommitVersion(version, idealhistory);
            }
        }

        private void CommitVersion(T version, List<T> history)
        {
            if (!UncommitedVersions.Contains(version))
                return;
            var parent = Parent(version, history);
            if (parent == null)
            {
                Console.WriteLine($"{version.VersionName} is the first version in the history!");
                InitialCommit(version);
            }
            else
            {
                Console.WriteLine($"Parent of {version.VersionName} is {parent.VersionName}");
                CommitVersion(parent, history);
                InsertCommit(version, parent);
            }
        }

        protected string SubtractMajorVersion(string release)
        {
            var match = ReleaseRegex.Match(release);
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                int minor = int.Parse(match.Groups[2].Value);
                return prefix + "1." + (minor - 1).ToString();
            }
            return null;
        }

        protected abstract IEnumerable<T> GetAllVersions();

        private string GetBranchName(T version)
        {
            return version.ReleaseName.Replace(' ', '-');
        }

        // commit stuff common to all commit methods
        private void DoCommit(T version)
        {
            // extract
            string workspace = Path.Combine(Path.GetTempPath(), "mc_version_history_workspace");
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, true);
            Directory.CreateDirectory(workspace);
            Console.WriteLine($"Extracting {version}");
            version.ExtractData(workspace);
            Console.WriteLine($"Translating NBT files...");
            TranslateNbtFiles(workspace);
            MergeWithWorkspace(RepoFolder, workspace);
            Directory.Delete(workspace, true);
            Util.RemoveEmptyFolders(RepoFolder);
            File.WriteAllText(Path.Combine(RepoFolder, "version.txt"), version.VersionName);
            // commit
            Console.WriteLine($"Committing...");
            CommandRunner.RunCommand(RepoFolder, $"git add -A");
            CommandRunner.RunCommand(RepoFolder, $"set GIT_COMMITTER_DATE={version.ReleaseTime} & git commit --date=\"{version.ReleaseTime}\" -m \"{version.VersionName}\"");
            // cleanup
            Console.WriteLine($"Cleaning up...");
            UncommittedVersionList.Remove(version);
            string hash = CommandRunner.RunCommand(RepoFolder, $"git log --all --grep=\"^{Regex.Escape(version.VersionName)}$\"", output: true).Output;
            hash = hash.Substring("commit ".Length, 40);
            CommittedVersionDict.Add(version, hash);
            CommandRunner.RunCommand(RepoFolder, $"git gc --prune=now --aggressive");
            CommandRunner.RunCommand(RepoFolder, $"git repack");
        }

        private void MergeWithWorkspace(string base_folder, string workspace)
        {
            // delete files that are not present in workspace
            foreach (var item in Directory.GetFiles(base_folder, "*", SearchOption.AllDirectories))
            {
                string relative = Util.RelativePath(base_folder, item);
                if (relative.StartsWith(".git"))
                    continue;
                string workspace_version = Path.Combine(workspace, relative);
                if (!File.Exists(workspace_version))
                    File.Delete(item);
            }

            // copy new/changed files from workspace
            foreach (var item in Directory.GetFiles(workspace, "*", SearchOption.AllDirectories))
            {
                string relative = Util.RelativePath(workspace, item);
                string base_version = Path.Combine(base_folder, relative);
                if (!File.Exists(base_version) || !Util.FilesAreEqual(new FileInfo(item), new FileInfo(base_version)))
                    Util.Copy(item, base_version);
                File.Delete(item);
            }
        }

        // for committing the first version
        private void InitialCommit(T version)
        {
            Console.WriteLine("Initializing repo...");
            CommandRunner.RunCommand(RepoFolder, $"git init");
            File.WriteAllText(Path.Combine(RepoFolder, ".gitignore"), "*.ini");
            CommandRunner.RunCommand(RepoFolder, $"git add -A");
            CommandRunner.RunCommand(RepoFolder, $"git commit -m \"Initial commit\"");
            // create branch
            var branchname = GetBranchName(version);
            CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\"");
            CommandRunner.RunCommand(RepoFolder, $"git checkout \"{branchname}\"");
            DoCommit(version);
        }

        // for committing a version after an existing one
        private void InsertCommit(T version, T parent)
        {
            Console.WriteLine($"Starting commit of {version}");
            // find commit hash for existing version
            string hash = CommittedVersionDict[parent];
            // create branch
            var branchname = GetBranchName(version);
            CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\" {hash}");
            // if this commit is the most recent for this branch, we can just commit right on top without insertion logic
            string tophash = CommandRunner.RunCommand(RepoFolder, $"git rev-parse \"{branchname}\"", output: true).Output;
            tophash = tophash.Substring(0, 40);
            if (hash == tophash)
            {
                Console.WriteLine($"On top, ready to go");
                CommandRunner.RunCommand(RepoFolder, $"git checkout \"{branchname}\"");
                DoCommit(version);
            }
            else
            {
                Console.WriteLine($"Needs to insert into history for this one");
                // make a branch that starts there and prepare to commit to it
                CommandRunner.RunCommand(RepoFolder, $"git checkout -b temp {hash}");
                CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\"");
                DoCommit(version);
                // insert
                Console.WriteLine($"Commit done, beginning rebase");
                CommandRunner.RunCommand(RepoFolder, $"git rebase --strategy-option theirs temp \"{branchname}\"");
                CommandRunner.RunCommand(RepoFolder, $"git branch -d temp");
                Console.WriteLine($"Rebase complete");
            }
        }

        // read all NBT files in the version and write textual copies
        private void TranslateNbtFiles(string root_folder)
        {
            string translations_path = Path.Combine(root_folder, "nbt_translations");
            Directory.CreateDirectory(translations_path);
            // don't enumerate because we're creating new directories as we go
            foreach (var directory in Directory.GetDirectories(root_folder, "*", SearchOption.AllDirectories))
            {
                string dest_folder = Path.Combine(translations_path, directory.Substring(root_folder.Length + 1));
                // bool any_nbts = false;

                foreach (var nbtpath in Directory.EnumerateFiles(directory, "*.nbt", SearchOption.TopDirectoryOnly))
                {
                    // any_nbts = true;
                    // remove DataVersion that makes diffs hard to read
                    var file = new NbtFile(nbtpath);
                    file.RootTag.Remove("DataVersion");
                    file.SaveToFile(nbtpath, file.FileCompression);

                    // custom method (matches vanilla really well, maintains order)
                    Directory.CreateDirectory(dest_folder);
                    File.WriteAllText(Path.Combine(dest_folder, Path.ChangeExtension(Path.GetFileName(nbtpath), ".snbt")), file.RootTag.ToSnbt(true) + "\n");
                }

                foreach (var bedrock_structure in Directory.EnumerateFiles(directory, "*.mcstructure", SearchOption.TopDirectoryOnly))
                {
                    // any_nbts = true;
                    var file = new NbtFile();
                    file.BigEndian = false;
                    file.LoadFromFile(bedrock_structure);
                    Directory.CreateDirectory(dest_folder);
                    File.WriteAllText(Path.Combine(dest_folder, Path.ChangeExtension(Path.GetFileName(bedrock_structure), ".mcstructure_str")), file.RootTag.ToSnbt(true) + "\n");
                }

                // data generator method (has annoying arbitrary order that doesn't match actual file)
                // if (any_nbts)
                // {
                //     Console.WriteLine($"Translating NBT files in top-level of {directory}");
                //     Directory.CreateDirectory(dest_folder);
                //     CommandRunner.RunCommand(Path.GetDirectoryName(JavaVersion.NbtTranslationJar), $"\"{JavaVersion.JavaPath}\" -cp \"{JavaVersion.NbtTranslationJar}\" net.minecraft.data.Main --dev --input \"{directory}\"");
                //     foreach (var item in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(JavaVersion.NbtTranslationJar), "generated"), "*.snbt", SearchOption.TopDirectoryOnly))
                //     {
                //         File.Move(item, Path.Combine(dest_folder, Path.GetFileName(item)));
                //     }
                // }
            }
        }
    }

    public class JavaUpdater : Updater<JavaVersion>
    {
        public static JObject VersionFacts;
        public JavaUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {
        }

        protected override IEnumerable<JavaVersion> GetAllVersions()
        {
            foreach (var folder in Directory.EnumerateDirectories(VersionsFolder))
            {
                var version = new JavaVersion(folder);
                bool should_load = true;
                foreach (string item in (JArray)VersionFacts["skip_contains"])
                {
                    if (version.VersionName.IndexOf(item, StringComparison.OrdinalIgnoreCase) != -1)
                        should_load = false;
                }
                if (should_load)
                    yield return new JavaVersion(folder);
            }
        }

        protected override JavaVersion SpecialParent(JavaVersion version, List<JavaVersion> history)
        {
            if ((VersionFacts["parents"]["map"] as JObject).TryGetValue(version.ReleaseName, out var parent))
                return history.LastOrDefault(x => x.ReleaseName == (string)parent);
            return base.SpecialParent(version, history);
        }

        protected override bool NoParentsAllowed(JavaVersion version)
        {
            foreach (string item in (JArray)VersionFacts["parents"]["skip_contains"])
            {
                if (version.ReleaseName.Contains(item))
                    return true;
            }
            return base.NoParentsAllowed(version);
        }
    }

    public class BedrockUpdater : Updater<BedrockVersion>
    {
        public BedrockUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {

        }

        protected override IEnumerable<BedrockVersion> GetAllVersions()
        {
            foreach (var zip in Directory.EnumerateFiles(VersionsFolder, "*.zip"))
            {
                yield return new BedrockVersion(zip);
            }
        }

        protected override BedrockVersion SpecialParent(BedrockVersion version, List<BedrockVersion> history)
        {
            if (version.ReleaseName == "1.4")
                return history.LastOrDefault(x => x.ReleaseName == "1.2");
            return base.SpecialParent(version, history);
        }
    }

    public abstract class Version
    {
        public DateTime ReleaseTime { get; protected set; }
        // example: 15w33a or 1.7.0.5
        public string VersionName { get; protected set; }
        // corresponding: 1.9 or 1.7
        public string ReleaseName { get; protected set; }

        public override string ToString()
        {
            return $"{this.GetType().Name} {VersionName} for {ReleaseName}, released {ReleaseTime}";
        }

        public abstract void ExtractData(string output);
    }

    public class JavaVersion : Version
    {
        private readonly string JarPath;
        private readonly string ServerJarURL;
        public static string JavaPath;
        public static string NbtTranslationJar;
        public static string ServerJarFolder;
        public static string DecompilerFile;
        public static JObject ReleasesMap;
        private static readonly Regex SnapshotRegex = new Regex(@"(\d\d)w(\d\d)[a-z~]");
        private static readonly DateTime DataGenerators = new DateTime(2018, 1, 1);
        private static readonly DateTime AssetGenerators = new DateTime(2020, 3, 1);
        private static readonly DateTime Mappings = new DateTime(2019, 9, 4);
        private static readonly string[] ModelOrder = new string[] { "model", "x", "y", "z", "uvlock", "weight" };
        private static readonly string[] IllegalNames = new[] { "aux", "con", "clock$", "nul", "prn", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
        public JavaVersion(string folder)
        {
            VersionName = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, VersionName + ".json");
            JarPath = Path.Combine(folder, VersionName + ".jar");
            JObject json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            ReleaseName = GetMadeForRelease(VersionName);
            ServerJarURL = (string)json["downloads"]?["server"]?["url"];
        }

        public override void ExtractData(string output)
        {
            if (ReleaseTime > DataGenerators)
            {
                Console.WriteLine("Fetching data reports...");
                string reports_path = Path.Combine(ServerJarFolder, "generated");
                if (Directory.Exists(reports_path))
                    Directory.Delete(reports_path, true);

                var serverjar = Path.Combine(ServerJarFolder, VersionName + ".jar");
                if (!File.Exists(serverjar))
                {
                    Console.WriteLine("Downloading server jar...");
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(ServerJarURL, serverjar);
                    }
                    Console.WriteLine("Download complete!");
                }
                var run = CommandRunner.RunCommand(ServerJarFolder, $"\"{JavaVersion.JavaPath}\" -cp \"{serverjar}\" net.minecraft.data.Main --reports");
                var outputfolder = Path.Combine(output, "reports");
                Directory.CreateDirectory(outputfolder);

                Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), outputfolder);
            }
            bool remap = ReleaseTime > Mappings && !VersionName.Contains("1.14_combat");
            Console.WriteLine("Decompiling source...");
            if (remap)
                Console.WriteLine("(with mappings)");
            string decompiler_folder = Path.GetDirectoryName(DecompilerFile);
            string python_name = Path.GetFileName(DecompilerFile);
            string python_config = "{\\\"version\\\":\\\"" + VersionName + "\\\",\\\"remap\\\":" + remap.ToString().ToLower() + "}";
            string command = $"python {python_name} {python_config}";
            CommandRunner.RunCommand(decompiler_folder, command);
            if (remap)
                File.Copy(Path.Combine(decompiler_folder, "mappings", VersionName, "mappings.txt"), Path.Combine(output, "mappings.txt"));
            Console.WriteLine("Copying source...");

            // cfr
            Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(Path.Combine(decompiler_folder, "src", VersionName), Path.Combine(output, "source"));

            // fernflower (disabled for now)
            // using (ZipArchive zip = ZipFile.OpenRead(Path.Combine(decompiler_path, "src", VersionName, VersionName + "-temp.jar")))
            // {
            //     foreach (var entry in zip.Entries)
            //     {
            //         if (entry.FullName.StartsWith("com") || entry.FullName.StartsWith("net"))
            //         {
            //             Directory.CreateDirectory(Path.Combine(output, "source", Path.GetDirectoryName(entry.FullName)));
            //             entry.ExtractToFile(Path.Combine(output, "source", entry.FullName));
            //         }
            //     }
            // }

            Console.WriteLine("Extracting jar...");
            using (ZipArchive zip = ZipFile.OpenRead(JarPath))
            {
                foreach (var entry in zip.Entries)
                {
                    string filename = Path.GetFileName(entry.FullName);
                    if (entry.FullName.EndsWith("/") || IllegalNames.Contains(filename.ToLower()) || filename.EndsWith(".class") || filename.EndsWith(".xml") || entry.FullName.Contains("META-INF"))
                        continue;
                    Directory.CreateDirectory(Path.Combine(output, "jar", Path.GetDirectoryName(entry.FullName)));
                    var destination = Path.Combine(output, "jar", entry.FullName);
                    entry.ExtractToFile(destination);
                    // sort special files that are arbitrarily ordered with each extraction
                    if (entry.FullName == "data/minecraft/advancements/nether/all_effects.json" ||
                        entry.FullName == "data/minecraft/advancements/nether/all_potions.json")
                    {
                        var advancement = JObject.Parse(File.ReadAllText(destination));
                        Util.SortKeys((JObject)advancement["criteria"]["all_effects"]["conditions"]["effects"]);
                        File.WriteAllText(destination, Util.ToMinecraftJson(advancement));
                    }
                    else if (entry.FullName == "data/minecraft/loot_tables/chests/shipwreck_supply.json")
                    {
                        var table = JObject.Parse(File.ReadAllText(destination));
                        var stew = ((JArray)table["pools"][0]["entries"]).FirstOrDefault(x => x["name"].ToString() == "minecraft:suspicious_stew");
                        if (stew != null)
                        {
                            var function = (JObject)stew["functions"][0];
                            function["effects"] = new JArray(((JArray)function["effects"]).OrderBy(x => x["type"].ToString()));
                            File.WriteAllText(destination, Util.ToMinecraftJson(table));
                        }
                    }
                    else if (ReleaseTime > AssetGenerators && Path.GetDirectoryName(entry.FullName) == @"assets\minecraft\blockstates")
                    {
                        var blockstate = JObject.Parse(File.ReadAllText(destination));
                        if (blockstate.TryGetValue("variants", out var variants))
                        {
                            foreach (var variant in (JObject)variants)
                            {
                                if (variant.Value is JArray many)
                                {
                                    foreach (JObject option in many)
                                    {
                                        Util.SortKeys(option, ModelOrder);
                                    }
                                }
                                else
                                    Util.SortKeys((JObject)variant.Value, ModelOrder);
                            }
                        }
                        else if (blockstate.TryGetValue("multipart", out var multipart))
                        {
                            foreach (JObject part in (JArray)multipart)
                            {
                                var apply = part["apply"];
                                if (apply is JArray many)
                                {
                                    foreach (JObject item in many)
                                    {
                                        Util.SortKeys(item, ModelOrder);
                                    }
                                }
                                else
                                    Util.SortKeys((JObject)apply, ModelOrder);
                                if (part.TryGetValue("when", out var when))
                                {
                                    if (((JObject)when).TryGetValue("OR", out var or))
                                    {
                                        foreach (JObject option in or)
                                        {
                                            Util.SortKeys(option);
                                        }
                                    }
                                    else
                                        Util.SortKeys((JObject)when);
                                }
                            }
                        }
                        File.WriteAllText(destination, Util.ToMinecraftJson(blockstate));
                    }
                }
            }
        }

        // facts of versions
        private string GetMadeForRelease(string versionname)
        {
            // possible formats:
            // 1.x.x        1.x
            // a1.x.x       Alpha 1.x
            // b1.x.x       Beta 1.x
            // c1.x.x       Alpha 1.x
            // inf-xxxx     Infdev
            // rd-xxxx      Classic
            // yywxxl       (needs lookup)

            if ((ReleasesMap["special"] as JObject).TryGetValue(versionname, out var release))
                return (string)release;

            // real versions
            if (versionname.StartsWith("1."))
                return MajorMinor(versionname);
            if (versionname.StartsWith("a1."))
                return "Alpha " + MajorMinor(versionname.Substring(1));
            if (versionname.StartsWith("b1."))
                return "Beta " + MajorMinor(versionname.Substring(1));
            if (versionname.StartsWith("inf-"))
                return "Infdev";
            if (versionname.StartsWith("in-"))
                return "Indev";
            if (versionname.StartsWith("c"))
                return "Classic";
            if (versionname.StartsWith("rd-"))
                return "Pre-Classic";

            var match = SnapshotRegex.Match(versionname);
            if (match.Success)
            {
                int year = int.Parse(match.Groups[1].Value);
                int week = int.Parse(match.Groups[2].Value);

                foreach (var snapshot in (JObject)ReleasesMap["snapshots"])
                {
                    string[] parts = snapshot.Key.Split('.');
                    int template_year = int.Parse(parts[0]);
                    int template_week = int.Parse(parts[1]);
                    if (year == template_year && week <= template_week)
                        return (string)snapshot.Value;
                }
            }
            throw new ArgumentException($"Could not determine the version to which {versionname} belongs");
        }

        private static string MajorMinor(string versionname)
        {
            if (versionname.Count(x => x == '.') < 2)
            {
                var ends = new[] { '-', ' ', '_', 'a', 'b', 'c', 'd' };
                int bestresult = int.MaxValue;
                string final = versionname;
                foreach (var end in ends)
                {
                    int index = versionname.IndexOf(end);
                    if (index != -1 && index < bestresult)
                    {
                        bestresult = index;
                        final = versionname.Substring(0, index);
                    }
                }
                return final;
            }
            return versionname.Substring(0, versionname.IndexOf('.', versionname.IndexOf('.') + 1));
        }
    }

    public class BedrockVersion : Version
    {
        private readonly string ZipPath;
        public BedrockVersion(string zippath)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zippath))
            {
                ZipPath = zippath;
                var mainappx = GetMainAppx(zip);
                VersionName = Path.GetFileName(mainappx.FullName).Split('_')[1];
                ReleaseName = VersionName.Substring(0, VersionName.IndexOf('.', VersionName.IndexOf('.') + 1));
                ReleaseTime = zip.Entries[0].LastWriteTime.UtcDateTime;
            }
        }

        public override void ExtractData(string output)
        {
            string appxpath = Path.Combine(output, "appx.appx");
            using (ZipArchive zip = ZipFile.OpenRead(ZipPath))
            {
                var appx = GetMainAppx(zip);
                appx.ExtractToFile(appxpath);
            }

            using (ZipArchive zip = ZipFile.OpenRead(appxpath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("data/") && Path.GetExtension(entry.FullName) != ".zip")
                    {
                        Directory.CreateDirectory(Path.Combine(output, Path.GetDirectoryName(entry.FullName)));
                        entry.ExtractToFile(Path.Combine(output, entry.FullName));
                    }
                }
            }
            var merged = Path.Combine(output, "latest_packs");
            var latest_behavior = Path.Combine(merged, "behavior_pack");
            var latest_resource = Path.Combine(merged, "resource_pack");
            Directory.CreateDirectory(merged);
            Directory.CreateDirectory(latest_behavior);
            Directory.CreateDirectory(latest_resource);
            var bpacks = GetVanillaPacks(Path.Combine(output, "data", "behavior_packs"));
            var rpacks = GetVanillaPacks(Path.Combine(output, "data", "resource_packs"));
            OverwriteAndMerge(bpacks, latest_behavior);
            OverwriteAndMerge(rpacks, latest_resource);
            File.Delete(appxpath);
        }

        private void OverwriteAndMerge(IEnumerable<string> sourcepacks, string destination_folder)
        {
            Console.WriteLine("Merging vanilla packs");
            foreach (var pack in sourcepacks)
            {
                Console.WriteLine($"Applying pack {Path.GetFileName(pack)}");
                foreach (var file in Directory.GetFiles(pack, "*.*", SearchOption.AllDirectories))
                {
                    var relative = Util.RelativePath(pack, file);
                    var dest = Path.Combine(destination_folder, relative);
                    var pieces = Util.Split(relative);
                    var first = pieces.First();
                    var last = pieces.Last();
                    var extension = Path.GetExtension(last);
                    bool handled = false;

                    if (pieces.Length == 1) // stuff in root
                    {
                        if (first == "blocks.json")
                        {
                            MergeJsons(dest, file, ObjectStraightFrom, x => x);
                            handled = true;
                        }
                        else if (first == "biomes_client.json")
                        {
                            MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "biomes"), x =>
                             new JObject() { { "biomes", x } });
                            handled = true;
                        }
                        else if (first == "items_offsets_client.json")
                        {
                            MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "render_offsets"), x =>
                             new JObject() { { "render_offsets", x } });
                            handled = true;
                        }
                        else if (first == "sounds.json" && File.Exists(dest))
                        {
                            var existing = JObject.Parse(File.ReadAllText(dest));
                            var incoming = JObject.Parse(File.ReadAllText(file));
                            var both = new Dictionary<JObject, List<JObject>> { { existing, new List<JObject>() }, { incoming, new List<JObject>() } };
                            foreach (var item in both.Keys)
                            {
                                var stuff = both[item];
                                stuff.Add((JObject)PathTo(item, "individual_event_sounds", "events") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "entity_sounds", "defaults") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "entity_sounds", "entities") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "block_sounds") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "interactive_sounds", "block_sounds") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "interactive_sounds", "entity_sounds", "defaults") ?? new JObject());
                                stuff.Add((JObject)PathTo(item, "interactive_sounds", "entity_sounds", "entities") ?? new JObject());
                            }
                            foreach (var item in both[existing].Zip(both[incoming], (x, y) => Tuple.Create(x, y)))
                            {
                                MergeJsons(item.Item1, item.Item2);
                            }
                            handled = true;
                        }
                    }
                    else
                    {
                        if (first == "sounds")
                        {
                            if (last == "sound_definitions.json")
                            {
                                MergeJsons(dest, file, SoundDefinitionsFrom, x =>
                                 new JObject() { { "format_version", "1.14.0" }, { "sound_definitions", x } });
                                handled = true;
                            }
                            else if (last == "music_definitions.json")
                            {
                                MergeJsons(dest, file, ObjectStraightFrom, x => x);
                                handled = true;
                            }
                        }
                        else if (first == "textures")
                        {
                            if (last == "flipbook_textures.json")
                            {
                                MergeJsons(dest, file, ArrayStraightFrom, x => x);
                                handled = true;
                            }
                            else if (last == "item_texture.json")
                            {
                                MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "texture_data"), x =>
                                    new JObject() { { "resource_pack_name", "vanilla" }, { "texture_name", "atlas.items" }, { "texture_data", x } });
                                handled = true;
                            }
                            else if (last == "terrain_texture.json")
                            {
                                MergeJsons(dest, file, x => (JObject)PathTo(ObjectStraightFrom(x), "texture_data"), x =>
                                 new JObject() { { "resource_pack_name", "vanilla" }, { "texture_name", "atlas.terrain" }, { "padding", 8 }, { "num_mip_levels", 4 }, { "texture_data", x } });
                                handled = true;
                            }
                        }
                    }
                    if (!handled)
                        Util.Copy(file, dest);
                }
            }
        }

        private JToken PathTo(JObject top, params string[] subs)
        {
            foreach (var item in subs.Take(subs.Length - 1))
            {
                if (top.TryGetValue(item, out var sub) && sub is JObject obj)
                    top = obj;
                else
                    return null;
            }
            if (top.TryGetValue(subs.Last(), out var final))
                return final;
            else
                return null;
        }

        private JObject MergeJsons(JObject existing, JObject incoming)
        {
            foreach (var item in incoming)
            {
                existing[item.Key] = item.Value;
            }
            return existing;
        }

        private void MergeJsons(string existing_path, string incoming_path, Func<string, JObject> loader, Func<JObject, JObject> transformer)
        {
            if (!File.Exists(existing_path))
                Util.Copy(incoming_path, existing_path);
            else
            {
                var existing = loader(existing_path);
                var incoming = loader(incoming_path);
                existing = MergeJsons(existing, incoming);
                var final = transformer(existing);
                File.WriteAllText(existing_path, Util.ToMinecraftJson(final));
            }
        }

        private void MergeJsons(string existing_path, string incoming_path, Func<string, JArray> loader, Func<JArray, JArray> transformer)
        {
            if (!File.Exists(existing_path))
                Util.Copy(incoming_path, existing_path);
            else
            {
                var existing = loader(existing_path);
                var incoming = loader(incoming_path);
                foreach (var item in incoming)
                {
                    existing.Add(item);
                }
                var final = transformer(existing);
                File.WriteAllText(existing_path, Util.ToMinecraftJson(final));
            }
        }

        private JObject SoundDefinitionsFrom(string filepath)
        {
            var jobj = ObjectStraightFrom(filepath);
            JObject definitions;
            if (jobj.TryGetValue("sound_definitions", out var def))
                definitions = (JObject)def;
            else
                definitions = jobj;
            return definitions;
        }

        private JObject ObjectStraightFrom(string filepath)
        {
            var jobj = JObject.Parse(File.ReadAllText(filepath), new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
            return jobj;
        }

        private JArray ArrayStraightFrom(string filepath)
        {
            var jarr = JArray.Parse(File.ReadAllText(filepath), new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
            return jarr;
        }

        private IEnumerable<string> GetVanillaPacks(string packsfolder)
        {
            var vanilla = Path.Combine(packsfolder, "vanilla");
            if (Directory.Exists(vanilla))
                yield return vanilla;
            var rest = new List<Match>();
            foreach (var directory in Directory.EnumerateDirectories(packsfolder))
            {
                var name = Path.GetFileName(directory);
                var match = Regex.Match(name, @"^vanilla_(\d+)\.(\d+)(\.(\d+))?$");
                if (match.Success)
                    rest.Add(match);
            }
            var sorted = rest.OrderBy(x => int.Parse(x.Groups[1].Value))
                .ThenBy(x => int.Parse(x.Groups[2].Value))
                .ThenBy(x => x.Groups[4].Success ? int.Parse(x.Groups[4].Value) : 0)
                .Select(x => x.Value);
            foreach (var item in sorted)
            {
                yield return Path.Combine(packsfolder, item);
            }
        }

        private ZipArchiveEntry GetMainAppx(ZipArchive zip)
        {
            foreach (var entry in zip.Entries)
            {
                string filename = Path.GetFileName(entry.FullName);
                // example: Minecraft.Windows_1.1.0.0_x64_UAP.Release.appx
                if (filename.StartsWith("Minecraft.Windows") && Path.GetExtension(filename) == ".appx")
                    return entry;
            }
            throw new FileNotFoundException($"Could not find main APPX");
        }
    }

    public class VersionComparer : IComparer<Version>
    {
        public static VersionComparer Instance = new VersionComparer();
        private VersionComparer() { }

        public int Compare(Version x, Version y)
        {
            if (x.ReleaseTime > y.ReleaseTime)
                return 1;
            if (x.ReleaseTime < y.ReleaseTime)
                return -1;
            var x_pieces = x.VersionName.Split('.').Where(o => int.TryParse(o, out _)).Select(int.Parse).ToList();
            var y_pieces = y.VersionName.Split('.').Where(o => int.TryParse(o, out _)).Select(int.Parse).ToList();
            for (int i = 0; i < Math.Min(x_pieces.Count, y_pieces.Count); i++)
            {
                int compare = x_pieces[i].CompareTo(y_pieces[i]);
                if (compare != 0)
                    return compare;
            }
            return 0;
        }
    }
}
