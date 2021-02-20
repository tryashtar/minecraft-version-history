using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class JavaUpdater : Updater
    {
        public readonly JavaConfig Config;
        public JavaUpdater(JavaConfig config)
        {
            Config = config;
        }

        protected override VersionGraph CreateGraph()
        {
            var versions = new List<Version>();
            foreach (var folder in Directory.EnumerateDirectories(Config.InputFolder))
            {
                versions.Add(new JavaVersion(folder));
            }
            return new VersionGraph(Config.VersionFacts, versions);
        }
    }
}
