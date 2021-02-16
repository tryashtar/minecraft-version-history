using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Minecraft_Version_History
{
    public abstract class Config
    {
        public readonly string InputFolder;
        public readonly string OutputRepo;
        public readonly VersionFacts VersionFacts;
        public Config(YamlMappingNode yaml)
        {
            InputFolder = (string)yaml["version folder"];
            OutputRepo = (string)yaml["repo"];
            VersionFacts = CreateVersionFacts(yaml["version facts"] as YamlMappingNode);
        }

        protected abstract VersionFacts CreateVersionFacts(YamlMappingNode yaml);
    }
}
