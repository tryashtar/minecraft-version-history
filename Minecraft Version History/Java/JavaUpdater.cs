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
            var graph = new VersionGraph(Config);
            foreach (var folder in Directory.EnumerateDirectories(Config.InputFolder))
            {
                var version = new JavaVersion(folder);
                if (Config.VersionFacts.ShouldSkip(version))
                    continue;
                var release = Config.VersionFacts.GetReleaseName(version);
                graph.Add(version, release);
            }
            Graph = graph;
#if DEBUG
            Console.WriteLine("New graph:");
            Console.WriteLine(graph.ToString());
            Console.ReadLine();
#endif
        }

        public void Perform()
        {

        }
    }
}
