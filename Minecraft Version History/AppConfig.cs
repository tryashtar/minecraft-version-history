using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class AppConfig
    {
        public readonly JavaConfig Java;
        public readonly BedrockConfig Bedrock;
        public readonly string GitInstallationPath;
        public readonly string GitIgnoreContents;
        public AppConfig(string folder, YamlMappingNode yaml)
        {
            GitInstallationPath = Path.Combine(folder, (string)yaml["git install"]);
            GitIgnoreContents = (string)yaml["gitignore"];
            Java = new JavaConfig(folder, yaml["java"] as YamlMappingNode);
            Bedrock = new BedrockConfig(yaml["bedrock"] as YamlMappingNode);
        }
    }
}
