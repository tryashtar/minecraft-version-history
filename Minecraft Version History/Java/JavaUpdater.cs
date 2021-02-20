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
        public readonly JavaConfig JavaConfig;
        protected override Config Config => JavaConfig;
        public JavaUpdater(JavaConfig config)
        {
            JavaConfig = config;
        }

        protected override VersionGraph CreateGraph()
        {
            var versions = new List<Version>();
            foreach (var folder in Directory.EnumerateDirectories(JavaConfig.InputFolder))
            {
                versions.Add(new JavaVersion(folder));
            }
            return new VersionGraph(JavaConfig.VersionFacts, versions);
        }
    }
}
