using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
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
