using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class BedrockUpdater
    {
        public readonly BedrockConfig Config;
        public VersionGraph Graph { get; private set; }
        public BedrockUpdater(BedrockConfig config)
        {
            Config = config;
            BuildGraph();
        }

        private void BuildGraph()
        {
            var graph = new VersionGraph();
            foreach (var folder in Directory.EnumerateDirectories(Config.InputFolder))
            {
                var version = new BedrockVersion(folder);
                if (Config.VersionFacts.ShouldSkip(version))
                    continue;
                var release = Config.VersionFacts.GetReleaseName(version);
                graph.Add(version, release);
            }
            Graph = graph;
        }

        public void Perform()
        {

        }
    }
}
