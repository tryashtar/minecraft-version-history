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

namespace Minecraft_Version_History
{
    public abstract class Updater<T> where T : Version
    {
        protected string RepoFolder { get; private set; }
        protected string VersionsFolder { get; private set; }
        protected List<T> CommittedVersions;
        protected List<T> UncommittedVersions;
        public Updater(string repo_folder, string versions_folder)
        {
            RepoFolder = repo_folder;
            VersionsFolder = versions_folder;
            CommittedVersions = new List<T>();
            UncommittedVersions = new List<T>();
            string[] logged = File.ReadAllLines(Path.Combine(versions_folder, "logged.txt"));
            foreach (var version in GetAllVersions())
            {
                if (logged.Contains(version.VersionName))
                    CommittedVersions.Add(version);
                else
                    UncommittedVersions.Add(version);
            }
        }

        protected abstract IEnumerable<T> GetAllVersions();

        public void CommitChanges()
        {
            // for each uncommited version:
            // - check out right branch
            // - copy files and stuff (with some CopyVersion() abstract method)
            // - commit
            // - cleanup
        }

        protected abstract void LoadVersions();
    }

    public class JavaUpdater : Updater<JavaVersion>
    {
        public JavaUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {

        }

        protected override IEnumerable<JavaVersion> GetAllVersions()
        {
            foreach (var folder in Directory.GetDirectories(VersionsFolder))
            {
                yield return new JavaVersion(folder);
            }
        }

        protected override void LoadVersions()
        {
            throw new NotImplementedException();
        }
    }

    public class BedrockUpdater : Updater<BedrockVersion>
    {
        public BedrockUpdater(string repo_folder, string versions_folder) : base(repo_folder, versions_folder)
        {

        }

        protected override IEnumerable<BedrockVersion> GetAllVersions()
        {
            foreach (var zip in Directory.GetFiles(VersionsFolder, "*.zip"))
            {
                yield return new BedrockVersion(zip);
            }
        }

        protected override void LoadVersions()
        {
            throw new NotImplementedException();
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
            return $"{this.GetType()} {VersionName} for {MadeForRelease}, released {ReleaseTime}";
        }
    }

    public class JavaVersion : Version
    {
        public JavaVersion(string folder)
        {
            VersionName = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, VersionName + ".json");
            JObject json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            MadeForRelease = GetMadeForRelease(VersionName);
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
        public BedrockVersion(string zippath)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zippath))
            {
                var mainappx = GetMainAppx(zip);
                VersionName = Path.GetFileName(mainappx.FullName).Split('_')[1];
                MadeForRelease = VersionName.Substring(0, VersionName.IndexOf('.', VersionName.IndexOf('.') + 1));
                ReleaseTime = zip.Entries[0].LastWriteTime.UtcDateTime;
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
}
