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
            var graph = new VersionGraph();
            foreach (var folder in Directory.EnumerateDirectories(Config.InputFolder))
            {
                var info = new JavaVersionInfo(folder);
                if (Config.VersionFacts.ShouldSkip(info))
                    continue;
                var release = Config.GetReleaseName(info);
                graph.Add(info, release);
            }
            Graph = graph;
        }
    }
}
