namespace MinecraftVersionHistory;

public class BedrockConfig : VersionConfig
{
    public readonly PackMerger BehaviorMerger;
    public readonly PackMerger ResourceMerger;
    public BedrockConfig(string folder, YamlMappingNode yaml) : base(folder, yaml)
    {
        BehaviorMerger = new PackMerger((YamlMappingNode)yaml.Go("pack merging", "behavior"));
        ResourceMerger = new PackMerger((YamlMappingNode)yaml.Go("pack merging", "resource"));
    }

    protected override VersionFacts CreateVersionFacts(YamlMappingNode yaml)
    {
        return new VersionFacts(yaml);
    }
}
