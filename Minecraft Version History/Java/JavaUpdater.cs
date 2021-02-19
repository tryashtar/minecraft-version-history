using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class JavaUpdater
    {
        public readonly JavaConfig Config;
        public VersionGraph Graph { get; private set; }
        public JavaUpdater(JavaConfig config)
        {
            Config = config;
            BuildGraph();
        }

        private void BuildGraph()
        {
            var versions = new List<Version>();
            foreach (var folder in Directory.EnumerateDirectories(Config.InputFolder))
            {
                var version = new JavaVersion(folder);
                if (!Config.VersionFacts.ShouldSkip(version))
                    versions.Add(version);
            }
            Graph = new VersionGraph(Config, versions);
#if DEBUG
            Console.WriteLine("New graph:");
            Console.WriteLine(Graph.ToString());
            Console.ReadLine();
#endif
        }

        public void Perform()
        {

        }
    }
}
