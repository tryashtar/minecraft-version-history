using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class BedrockUpdater : Updater
    {
        public readonly BedrockConfig BedrockConfig;
        protected override Config Config => BedrockConfig;
        public BedrockUpdater(BedrockConfig config)
        {
            BedrockConfig = config;
        }

        protected override VersionGraph CreateGraph()
        {
            var versions = new List<Version>();
            foreach (var zip in Directory.EnumerateFiles(BedrockConfig.InputFolder))
            {
                if (Path.GetExtension(zip) == ".zip")
                    versions.Add(new BedrockVersion(zip));
            }
            return new VersionGraph(BedrockConfig.VersionFacts, versions);
        }
    }
}
