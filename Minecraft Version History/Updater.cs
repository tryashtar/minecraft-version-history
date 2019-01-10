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

namespace Minecraft_Version_History
{
    public static class CommandRunner
    {
        public static Process RunCommand(string cd, string input)
        {
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = cd;
            cmd.StartInfo.Arguments = $"/C {input}";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
            cmd.WaitForExit();
            return cmd;
        }
    }

    public abstract class Updater<T> where T : Version
    {
        protected string RepoFolder { get; private set; }
        protected string VersionsFolder { get; private set; }
        protected List<T> CommittedVersions;
        protected List<T> UncommittedVersions;
        private string LoggedVersionsPath;
        readonly static string[] GitFolders = new[] { ".git" };
        readonly static string[] GitFiles = new[] { ".gitignore", "version.txt" };
        public Updater(string repo_folder, string versions_folder)
        {
            RepoFolder = repo_folder;
            VersionsFolder = versions_folder;
            CommittedVersions = new List<T>();
            UncommittedVersions = new List<T>();
            LoggedVersionsPath = Path.Combine(versions_folder, "logged.txt");
            string[] logged = File.ReadAllLines(LoggedVersionsPath);
            foreach (var version in GetAllVersions())
            {
                if (logged.Contains(version.VersionName))
                    CommittedVersions.Add(version);
                else
                    UncommittedVersions.Add(version);
            }
        }

        public void CommitChanges()
        {
            var versioncomparer = new VersionComparer();
            var groupcomparer = new ReleaseComparer();
            // each group has its own git branch
            var groups = UncommittedVersions.GroupBy(x => x.MadeForRelease);
            foreach (var branch in groups.OrderBy(x => x.Key, groupcomparer))
            {
                Console.WriteLine($"Release version {branch.Key}");
                var branchname = branch.Key.Replace(' ', '-');
                CommandRunner.RunCommand(RepoFolder, $"git branch {branchname}");
                CommandRunner.RunCommand(RepoFolder, $"git checkout {branchname}");
                foreach (var version in branch.OrderBy(x => x, versioncomparer))
                {
                    Console.WriteLine($"Version {version}");
                    WipeFolderExcept(RepoFolder, GitFolders, GitFiles);
                    version.ExtractData(RepoFolder);
                    File.WriteAllText(Path.Combine(RepoFolder, "version.txt"), version.VersionName);
                    CommandRunner.RunCommand(RepoFolder, $"git add -A");
                    Console.WriteLine("Committing...");
                    CommandRunner.RunCommand(RepoFolder, $"git commit --date=\"{version.ReleaseTime}\" -m \"{version.VersionName}\"");
                    File.AppendAllText(LoggedVersionsPath, version.VersionName + Environment.NewLine);
                    CommandRunner.RunCommand(RepoFolder, $"git gc --prune=now --aggressive");
                    CommandRunner.RunCommand(RepoFolder, $"git repack");
                }
            }
            foreach (var version in UncommittedVersions)
            {
                CommittedVersions.Add(version);
            }
            UncommittedVersions.Clear();
        }

        protected abstract IEnumerable<T> GetAllVersions();

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
        public JavaUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {

        }

        protected override IEnumerable<JavaVersion> GetAllVersions()
        {
            foreach (var folder in Directory.EnumerateDirectories(VersionsFolder))
            {
                yield return new JavaVersion(folder);
            }
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
    }

    public abstract class Version
    {
        public DateTime ReleaseTime { get; protected set; }
        // example: 15w33a or 1.7.0.5
        public string VersionName { get; protected set; }
        // corresponding: 1.9 or 1.7
        public string MadeForRelease { get; protected set; }

        public override string ToString()
        {
            return $"{this.GetType().Name} {VersionName} for {MadeForRelease}, released {ReleaseTime}";
        }

        public virtual int CompareTo(Version other)
        {
            return DateTime.Compare(this.ReleaseTime, other.ReleaseTime);
        }

        public abstract void ExtractData(string output);
    }

    public class JavaVersion : Version
    {
        private string JarPath;
        public static string ServerJarFolder;
        private static DateTime DataGenerators = new DateTime(2018, 1, 1);
        private static readonly string[] IllegalNames = new[] { "aux", "con", "clock$", "nul", "prn", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
        public JavaVersion(string folder)
        {
            VersionName = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, VersionName + ".json");
            JarPath = Path.Combine(folder, VersionName + ".jar");
            JObject json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            MadeForRelease = GetMadeForRelease(VersionName);
        }

        public override void ExtractData(string output)
        {
            if (ReleaseTime > DataGenerators)
            {
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
                foreach (var report in Directory.EnumerateFiles(Path.Combine(ServerJarFolder, "generated", "reports")))
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
            var match = Regex.Match(versionname, @"(\d\d)w(\d\d)[a-z~]");
            if (match.Success)
            {
                string year = match.Groups[1].ToString();
                int week = int.Parse(match.Groups[2].ToString());
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
                    { Tuple.Create("19", 50), "1.14" },
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
                var ends = new[] { '-', ' ', '_' };
                foreach (var end in ends)
                {
                    int index = versionname.IndexOf(end);
                    if (index != -1)
                        return versionname.Substring(0, index);
                }
                return versionname;
            }
            return versionname.Substring(0, versionname.IndexOf('.', versionname.IndexOf('.') + 1));
        }
    }

    public class BedrockVersion : Version
    {
        private string ZipPath;
        public BedrockVersion(string zippath)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zippath))
            {
                ZipPath = zippath;
                var mainappx = GetMainAppx(zip);
                VersionName = Path.GetFileName(mainappx.FullName).Split('_')[1];
                MadeForRelease = VersionName.Substring(0, VersionName.IndexOf('.', VersionName.IndexOf('.') + 1));
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
        public int Compare(Version x, Version y)
        {
            return x.CompareTo(y);
        }
    }

    public class ReleaseComparer : IComparer<string>
    {
        // determine which release version came later
        // example inputs: "Alpha 0.4" and "1.12.2"
        public int Compare(string x, string y)
        {
            int x_era = Era(x);
            int y_era = Era(y);
            if (x_era > y_era)
                return 1;
            else if (x_era < y_era)
                return -1;
            if (x.Contains('.') && y.Contains('.'))
            {
                // convert something like "Alpha 0.2" to "0.2"
                x = x.Substring(x.LastIndexOf(' ') + 1);
                y = y.Substring(y.LastIndexOf(' ') + 1);
                return CompareSemVer(x, y);
            }
            return 0;
        }

        private int CompareSemVer(string x, string y)
        {
            // convert something like "1.7.10b" to ["1", "7", "10b"]
            string[] x_semver = x.Split('.');
            string[] y_semver = y.Split('.');
            for (int i = 0; i < Math.Min(x_semver.Length, y_semver.Length); i++)
            {
                // first try to compare numerically
                if (int.TryParse(x_semver[i], out int xsvi) && int.TryParse(y_semver[i], out int ysvi))
                {
                    if (xsvi > ysvi)
                        return 1;
                    else if (xsvi < ysvi)
                        return -1;
                }
                // if it has a letter or something just string compare
                else
                {
                    int strcompare = String.Compare(x_semver[i], y_semver[i]);
                    if (strcompare != 0)
                        return strcompare;
                }
            }
            if (x_semver.Length > y_semver.Length)
                return 1;
            else if (x_semver.Length < y_semver.Length)
                return -1;
            return 0;
        }

        private int Era(string input)
        {
            if (input.StartsWith("Classic"))
                return 0;
            if (input.StartsWith("Infdev"))
                return 1;
            if (input.StartsWith("Alpha"))
                return 2;
            if (input.StartsWith("Beta"))
                return 3;
            return 4;
        }
    }
}
