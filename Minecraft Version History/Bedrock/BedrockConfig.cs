using YamlDotNet.RepresentationModel;

namespace Minecraft_Version_History
{
    public class BedrockConfig
    {
        public readonly VersionFacts VersionFacts;
        public BedrockConfig(YamlMappingNode yaml)
        {
            VersionFacts = new VersionFacts(yaml["version facts"] as YamlMappingNode);
        }
    }
}
