using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class JavaUpdater2 : Updater2
    {
        public readonly JavaConfig Config;
        public List<JavaVersion> AllVersions { get; private set; }
        public JavaUpdater2(JavaConfig config)
        {
            Config = config;
            ScanVersions();
        }

        protected override string RepoFolder => Config.OutputRepo;
        protected override IEnumerable<Version> GetAllVersions() => AllVersions;

        private void ScanVersions()
        {
            AllVersions = new List<JavaVersion>();
            foreach (var folder in Directory.EnumerateDirectories(Config.InputFolder))
            {
                var version = new JavaVersion(folder);
                if (!Config.VersionFacts.ShouldSkip(version))
                    AllVersions.Add(new JavaVersion(folder));
            }
        }

        protected override Version GetParent(Version child)
        {
            string name = Config.VersionFacts.GetSpecialParent(child);
            if (name != null)
                return AllVersions.First(x => x.VersionName == name);

        }

        protected override string GetReleaseName(Version version)
        {
            throw new NotImplementedException();
        }
    }
}
