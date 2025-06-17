namespace MinecraftVersionHistory;

public class BedrockConfig : VersionConfig
{
    public readonly List<PackMerger> PackMergers;
    public BedrockConfig(string folder, AppConfig parent, YamlMappingNode yaml) : base(folder, parent, yaml)
    {
        PackMergers = yaml.Go("pack merging").ToList(x => new PackMerger((YamlMappingNode)x)) ?? new();
    }

    protected override VersionFacts CreateVersionFacts(YamlMappingNode yaml)
    {
        return new VersionFacts(yaml);
    }
}
