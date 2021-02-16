using YamlDotNet.RepresentationModel;

namespace Minecraft_Version_History
{
    public class BedrockConfig : Config
    {
        public BedrockConfig(YamlMappingNode yaml) : base(yaml)
        {
        }

        protected override VersionFacts CreateVersionFacts(YamlMappingNode yaml)
        {
            return new VersionFacts(yaml);
        }
    }
}
