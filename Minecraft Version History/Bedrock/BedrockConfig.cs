using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class BedrockConfig : Config
    {
        public readonly PackMerger BehaviorMerger;
        public readonly PackMerger ResourceMerger;
        public BedrockConfig(YamlMappingNode yaml) : base(yaml)
        {
            BehaviorMerger = new PackMerger((YamlMappingNode)yaml.Go("pack merging", "behavior"));
            ResourceMerger = new PackMerger((YamlMappingNode)yaml.Go("pack merging", "resource"));
        }

        protected override VersionFacts CreateVersionFacts(YamlMappingNode yaml)
        {
            return new VersionFacts(yaml);
        }
    }
}
