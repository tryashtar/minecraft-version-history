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
            foreach (var version in GetAllVersions())
            {
                string hash = CommandRunner.RunCommand(RepoFolder, $"git log --all --grep=\"^{Regex.Escape(version.VersionName)}$\"", output: true).Output;
                if (hash.Length == 0)
                {
                    UncommittedVersionList.Add(version);
                    Console.WriteLine($"New version: {version.VersionName}");
                }
                else
                {
                    hash = hash.Substring("commit ".Length, 40);
                    CommittedVersionDict.Add(version, hash);
                }
            }
        }

        public void CommitChanges()
        {
            var idealhistory = CommittedVersions.Concat(UncommitedVersions).OrderBy(x => x, VersionComparer.Instance).ToList();
            foreach (var version in idealhistory)
            {
                if (UncommitedVersions.Contains(version))
                {
                    int my_index = idealhistory.IndexOf(version);
                    var parent = idealhistory.LastOrDefault(x => version != x && version.ReleaseName == x.ReleaseName && version.ReleaseTime >= x.ReleaseTime && idealhistory.IndexOf(x) < my_index);
                    if (parent == null)
                    {
                        string parentrelease = GetParentRelease(version.ReleaseName);
                        parent = idealhistory.FirstOrDefault(x => version != x && x.ReleaseName == parentrelease && version.ReleaseTime >= x.ReleaseTime);
                    }
                    if (parent == null)
                    {
                        Console.WriteLine($"{version.VersionName} is the first version in the history!");
                        InitialCommit(version);
                    }
                    else
                    {
                        Console.WriteLine($"Parent of {version.VersionName} is {parent.VersionName}");
                        InsertCommit(version, parent);
                    }
                }
            }
        }

        protected abstract string GetParentRelease(string release);

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
            Console.WriteLine($"Wiping folder...");
            WipeFolderExcept(RepoFolder, GitFolders, GitFiles);
            Console.WriteLine($"Extracting {version}");
            version.ExtractData(RepoFolder);
            Console.WriteLine($"Translating NBT files...");
            TranslateNbtFiles();
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
                CommandRunner.RunCommand(RepoFolder, $"git checkout \"{branchname}\"");
                DoCommit(version);
            }
            else
            {
                // make a branch that starts there and prepare to commit to it
                CommandRunner.RunCommand(RepoFolder, $"git checkout -b temp {hash}");
                CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\"");
                DoCommit(version);
                // insert
                CommandRunner.RunCommand(RepoFolder, $"git rebase temp \"{branchname}\"");
                CommandRunner.RunCommand(RepoFolder, $"git branch -d temp");
            }
        }

        // read all NBT files in the version and write textual copies
        private void TranslateNbtFiles()
        {
            string translations_path = Path.Combine(RepoFolder, "nbt_translations");
            FreshFolder(translations_path);
            // don't enumerate because we're creating new directories as we go
            foreach (var directory in Directory.GetDirectories(RepoFolder, "*", SearchOption.AllDirectories))
            {
                string dest_folder = Path.Combine(translations_path, directory.Substring(RepoFolder.Length + 1));
                bool any_nbts = false;

                foreach (var nbtpath in Directory.EnumerateFiles(directory, "*.nbt", SearchOption.TopDirectoryOnly))
                {
                    any_nbts = true;
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
                    any_nbts = true;
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

        private static void FreshFolder(string folder)
        {
            Directory.CreateDirectory(folder);
            WipeFolderExcept(folder, new string[] { }, new string[] { });
        }

        private static void WipeFolderExcept(string folder, string[] keep_folders, string[] keep_files)
        {
            foreach (var subfolder in Directory.EnumerateDirectories(folder))
            {
                if (!keep_folders.Contains(Path.GetFileName(subfolder)))
                    Directory.Delete(subfolder, true);
            }
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (!keep_files.Contains(Path.GetFileName(file)))
                    File.Delete(file);
            }
        }
    }

    public class JavaUpdater : Updater<JavaVersion>
    {
        private static Dictionary<string, string> ParentReleaseDict;
        public JavaUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {
            ParentReleaseDict = new Dictionary<string, string>()
            {
                {"1.0", "Beta 1.9" },
                {"Beta 1.0", "Alpha 1.2" },
                {"Alpha 1.0", "Infdev" },
                {"Infdev", "Alpha 0.30" },
                {"Alpha 0.30", "Alpha 0.0" },
                {"Alpha 0.0", "Classic" },

                {"April Fools 2013", "1.5" },
                {"April Fools 2015", "1.8" },
                {"April Fools 2016", "1.9" },
                {"April Fools 2019", "1.14" },
                {"April Fools 2020", "1.16" },
                {"Combat Test 1", "1.14" },
                {"Combat Test 2", "1.14" },
                {"Combat Test 3", "Combat Test 2" },
                {"Combat Test 4", "1.15" },
                {"Combat Test 5", "1.15" },
            };
        }

        protected override IEnumerable<JavaVersion> GetAllVersions()
        {
            foreach (var folder in Directory.EnumerateDirectories(VersionsFolder))
            {
                var version = new JavaVersion(folder);
                if (version.VersionName.IndexOf("optifine", StringComparison.OrdinalIgnoreCase) == -1)
                    yield return new JavaVersion(folder);
            }
        }

        protected override string GetParentRelease(string release)
        {
            if (ParentReleaseDict.TryGetValue(release, out string result))
                return result;
            return SubtractMajorVersion(release);
        }
    }

    public class BedrockUpdater : Updater<BedrockVersion>
    {
        private static Dictionary<string, string> ParentReleaseDict;
        public BedrockUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {
            ParentReleaseDict = new Dictionary<string, string>()
            {
                { "1.4", "1.2" }
            };
        }

        protected override IEnumerable<BedrockVersion> GetAllVersions()
        {
            foreach (var zip in Directory.EnumerateFiles(VersionsFolder, "*.zip"))
            {
                yield return new BedrockVersion(zip);
            }
        }

        protected override string GetParentRelease(string release)
        {
            if (ParentReleaseDict.TryGetValue(release, out string result))
                return result;
            return SubtractMajorVersion(release);
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
        public static string JavaPath;
        public static string NbtTranslationJar;
        public static string ServerJarFolder;
        public static string DecompilerFile;
        private static Regex SnapshotRegex = new Regex(@"(\d\d)w(\d\d)[a-z~]");
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
        }

        public override void ExtractData(string output)
        {
            if (ReleaseTime > DataGenerators)
            {
                Console.WriteLine("Fetching data reports...");
                string reports_path = Path.Combine(ServerJarFolder, "generated");
                if (Directory.Exists(reports_path))
                    Directory.Delete(reports_path, true);
                TryWithAllServers($"\"{JavaVersion.JavaPath}\" -Xss1M -cp \"{JarPath}\";\"{{0}}\" net.minecraft.data.Main --reports");
                var outputfolder = Path.Combine(output, "reports");
                Directory.CreateDirectory(outputfolder);
                foreach (var report in Directory.EnumerateFiles(Path.Combine(reports_path, "reports")))
                {
                    var destination = Path.Combine(outputfolder, Path.GetFileName(report));
                    File.Copy(report, destination);
                }
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
                        SortKeys((JObject)advancement["criteria"]["all_effects"]["conditions"]["effects"]);
                        File.WriteAllText(destination, ToMinecraftJson(advancement));
                    }
                    else if (entry.FullName == "data/minecraft/loot_tables/chests/shipwreck_supply.json")
                    {
                        var table = JObject.Parse(File.ReadAllText(destination));
                        var stew = ((JArray)table["pools"][0]["entries"]).FirstOrDefault(x => x["name"].ToString() == "minecraft:suspicious_stew");
                        if (stew != null)
                        {
                            var function = (JObject)stew["functions"][0];
                            function["effects"] = new JArray(((JArray)function["effects"]).OrderBy(x => x["type"].ToString()));
                            File.WriteAllText(destination, ToMinecraftJson(table));
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
                                        SortKeys(option, ModelOrder);
                                    }
                                }
                                else
                                    SortKeys((JObject)variant.Value, ModelOrder);
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
                                        SortKeys(item, ModelOrder);
                                    }
                                }
                                else
                                    SortKeys((JObject)apply, ModelOrder);
                                if (part.TryGetValue("when", out var when))
                                {
                                    if (((JObject)when).TryGetValue("OR", out var or))
                                    {
                                        foreach (JObject option in or)
                                        {
                                            SortKeys(option);
                                        }
                                    }
                                    else
                                        SortKeys((JObject)when);
                                }
                            }
                        }
                        File.WriteAllText(destination, ToMinecraftJson(blockstate));
                    }
                }
            }
        }

        public static void TryWithAllServers(string command)
        {
            bool success = false;
            foreach (var serverjar in Directory.EnumerateFiles(ServerJarFolder, "*.jar"))
            {
                // data reports wipe the entire output folder they run in
                // so we need to put them somewhere safe and then copy
                var run = CommandRunner.RunCommand(ServerJarFolder, String.Format(command, serverjar), suppress_errors: true);
                if (run.ExitCode == 0)
                {
                    Console.WriteLine($"{Path.GetFileName(serverjar)} was compatible");
                    success = true;
                    break;
                }
                else
                    Console.WriteLine($"{Path.GetFileName(serverjar)} was incompatible");
            }
            if (!success)
                throw new FileNotFoundException("No compatible server jar found");
        }

        private static void SortKeys(JObject obj, string[] order = null)
        {
            var tokens = new List<KeyValuePair<string, JToken>>();
            foreach (var item in obj)
            {
                tokens.Add(item);
            }
            obj.RemoveAll();
            var ordered = order == null ? tokens.OrderBy(x => x.Key) : tokens.OrderBy(x =>
            {
                var index = Array.IndexOf(order, x.Key);
                return index < 0 ? int.MaxValue : index;
            }).ThenBy(x => x.Key);
            foreach (var item in ordered)
            {
                obj.Add(item.Key, item.Value);
            }
        }

        private static string ToMinecraftJson(JObject value)
        {
            StringBuilder sb = new StringBuilder(256);
            StringWriter sw = new StringWriter(sb, CultureInfo.InvariantCulture);

            var jsonSerializer = JsonSerializer.CreateDefault();
            using (JsonTextWriter jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.Formatting = Formatting.Indented;
                jsonWriter.IndentChar = ' ';
                jsonWriter.Indentation = 2;

                jsonSerializer.Serialize(jsonWriter, value, typeof(JObject));
            }

            return sw.ToString();
        }

        // facts of versions
        private static string GetMadeForRelease(string versionname)
        {
            // possible formats:
            // 1.x.x        1.x
            // a1.x.x       Alpha 1.x
            // b1.x.x       Beta 1.x
            // c1.x.x       Alpha 1.x
            // inf-xxxx     Infdev
            // rd-xxxx      Classic
            // yywxxl       (needs lookup)

            // special stuff first
            if (versionname == "1.14_combat-212796")
                return "Combat Test 1";
            if (versionname == "1.14_combat-0")
                return "Combat Test 2";
            if (versionname == "1.14_combat-3")
                return "Combat Test 3";
            if (versionname == "1.15_combat-1")
                return "Combat Test 4";
            if (versionname == "1.15_combat-6")
                return "Combat Test 5";
            if (versionname.StartsWith("April Fools 2.0"))
                return "April Fools 2013";
            if (versionname == "15w14a")
                return "April Fools 2015";
            if (versionname == "1.RV-Pre1")
                return "April Fools 2016";
            if (versionname == "3D Shareware v1.34")
                return "April Fools 2019";
            if (versionname == "20w14infinite")
                return "April Fools 2020";
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
                string year = match.Groups[1].Value;
                int week = int.Parse(match.Groups[2].Value);
                var snapshots = new Dictionary<Tuple<string, int>, string>
                {
                    // if a snapshot is in year A and on week B or earlier, it was for version C
                    { Tuple.Create("11", 50), "1.1" },
                    { Tuple.Create("12", 1), "1.1" },
                    { Tuple.Create("12", 8), "1.2" },
                    { Tuple.Create("12", 30), "1.3" },
                    { Tuple.Create("12", 50), "1.4" },
                    { Tuple.Create("13", 12), "1.5" },
                    { Tuple.Create("13", 26), "1.6" },
                    { Tuple.Create("13", 49), "1.7" },
                    { Tuple.Create("14", 34), "1.8" },
                    { Tuple.Create("15", 51), "1.9" },
                    { Tuple.Create("16", 15), "1.9" },
                    { Tuple.Create("16", 21), "1.10" },
                    { Tuple.Create("16", 50), "1.11" },
                    { Tuple.Create("17", 31), "1.12" },
                    { Tuple.Create("17", 50), "1.13" },
                    { Tuple.Create("18", 33), "1.13" },
                    { Tuple.Create("18", 50), "1.14" },
                    { Tuple.Create("19", 33), "1.14" },
                    { Tuple.Create("19", 50), "1.15" },
                    { Tuple.Create("20", 50), "1.16" },
                };
                foreach (var rule in snapshots)
                {
                    if (year == rule.Key.Item1 && week <= rule.Key.Item2)
                        return rule.Value;
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
            File.Delete(appxpath);
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
            var x_pieces = x.VersionName.Split('.').Where(o => int.TryParse(o, out _)).ToList();
            var y_pieces = y.VersionName.Split('.').Where(o => int.TryParse(o, out _)).ToList();
            for (int i = 0; i < Math.Min(x_pieces.Count, y_pieces.Count); i++)
            {
                int x_num = int.Parse(x_pieces[i]);
                int y_num = int.Parse(y_pieces[i]);
                int compare = x_num.CompareTo(y_num);
                if (compare != 0)
                    return compare;
            }
            return 0;
        }
    }
}
