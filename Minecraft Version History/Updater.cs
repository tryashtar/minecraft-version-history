﻿using System;
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

namespace Minecraft_Version_History
{
    public static class CommandRunner
    {
        public static Process RunCommand(string cd, string input, bool output = false)
        {
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = cd;
            cmd.StartInfo.Arguments = $"/C {input}";
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            if (output)
                cmd.StartInfo.RedirectStandardOutput = true;
            cmd.Start();
            cmd.WaitForExit();
            return cmd;
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
                string hash = CommandRunner.RunCommand(RepoFolder, $"git log --all --grep=\"^{Regex.Escape(version.VersionName)}$\"", output: true).StandardOutput.ReadToEnd();
                if (hash.Length == 0)
                {
                    UncommittedVersionList.Add(version);
                    Console.WriteLine($"New version -- {version.VersionName}");
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
            var idealhistory = CommittedVersions.Concat(UncommitedVersions).OrderBy(x => x.ReleaseTime);
            foreach (var version in idealhistory)
            {
                if (UncommitedVersions.Contains(version))
                {
                    var parent = idealhistory.LastOrDefault(x => version != x && version.ReleaseName == x.ReleaseName && version.ReleaseTime >= x.ReleaseTime);
                    if (parent == null)
                    {
                        string parentrelease = GetParentRelease(version.ReleaseName);
                        parent = idealhistory.LastOrDefault(x => version != x && x.ReleaseName == parentrelease && version.ReleaseTime >= x.ReleaseTime);
                    }
                    if (parent == null)
                        InitialCommit(version);
                    else
                        InsertCommit(version, parent);
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
            string hash = CommandRunner.RunCommand(RepoFolder, $"git log --all --grep=\"^{Regex.Escape(version.VersionName)}$\"", output: true).StandardOutput.ReadToEnd();
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
            string tophash = CommandRunner.RunCommand(RepoFolder, $"git rev-parse \"{branchname}\"", output: true).StandardOutput.ReadToEnd();
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
            foreach (var nbtpath in Directory.EnumerateFiles(RepoFolder, "*.nbt", SearchOption.AllDirectories))
            {
                var file = new NbtFile(nbtpath);
                string dest = Path.Combine(translations_path, nbtpath.Substring(RepoFolder.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.WriteAllText(Path.ChangeExtension(dest, ".snbt"), file.RootTag.ToSnbt(true));
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
                {"Combat Test 1", "1.14" },
                {"Combat Test 2", "1.14" }
            };
        }

        protected override IEnumerable<JavaVersion> GetAllVersions()
        {
            foreach (var folder in Directory.EnumerateDirectories(VersionsFolder))
            {
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
            { };
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
        public static string ServerJarFolder;
        private static Regex SnapshotRegex = new Regex(@"(\d\d)w(\d\d)[a-z~]");
        private static readonly DateTime DataGenerators = new DateTime(2018, 1, 1);
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
                string reports_path = Path.Combine(ServerJarFolder, "generated");
                if (Directory.Exists(reports_path))
                    Directory.Delete(reports_path, true);
                bool success = false;
                foreach (var serverjar in Directory.EnumerateFiles(ServerJarFolder, "*.jar"))
                {
                    // data reports wipe the entire output folder they run in
                    // so we need to put them somewhere safe and then copy
                    var run = CommandRunner.RunCommand(ServerJarFolder, $"java -Xss1M -cp \"{JarPath}\";\"{serverjar}\" net.minecraft.data.Main --reports");
                    if (run.ExitCode == 0)
                    {
                        success = true;
                        break;
                    }
                }
                if (!success)
                    throw new FileNotFoundException("No compatible server jar found to generate data reports");
                Directory.CreateDirectory(Path.Combine(output, "reports"));
                foreach (var report in Directory.EnumerateFiles(Path.Combine(reports_path, "reports")))
                {
                    File.Copy(report, Path.Combine(output, "reports", Path.GetFileName(report)));
                }
            }
            using (ZipArchive zip = ZipFile.OpenRead(JarPath))
            {
                foreach (var entry in zip.Entries)
                {
                    string filename = Path.GetFileName(entry.FullName);
                    if (entry.FullName.EndsWith("/") || IllegalNames.Contains(filename.ToLower()) || filename.EndsWith(".class") || filename.EndsWith(".xml") || entry.FullName.Contains("META-INF"))
                        continue;
                    Directory.CreateDirectory(Path.Combine(output, "jar", Path.GetDirectoryName(entry.FullName)));
                    entry.ExtractToFile(Path.Combine(output, "jar", entry.FullName));
                }
            }
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
            if (versionname.StartsWith("April Fools 2.0"))
                return "April Fools 2013";
            if (versionname == "15w14a")
                return "April Fools 2015";
            if (versionname == "1.RV-Pre1")
                return "April Fools 2016";
            if (versionname == "3D Shareware v1.34")
                return "April Fools 2019";
            // real versions
            if (versionname.StartsWith("1."))
                return MajorMinor(versionname);
            if (versionname.StartsWith("a1.") || versionname.StartsWith("c"))
                return "Alpha " + MajorMinor(versionname.Substring(1));
            if (versionname.StartsWith("b1."))
                return "Beta " + MajorMinor(versionname.Substring(1));
            if (versionname.StartsWith("inf-"))
                return "Infdev";
            if (versionname.StartsWith("rd-"))
                return "Classic";
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
}
