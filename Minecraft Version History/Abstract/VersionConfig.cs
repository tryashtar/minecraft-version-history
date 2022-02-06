namespace MinecraftVersionHistory;

public abstract class VersionConfig
{
    public readonly List<string> InputFolders;
    public readonly GitRepo GitRepo;
    public readonly VersionFacts VersionFacts;
    public readonly List<NbtTranslationOptions> NbtTranslations;
    public VersionConfig(string folder, AppConfig parent, YamlMappingNode yaml)
    {
        InputFolders = yaml.Go("version folders").ToList(x => Util.FilePath(folder, x));
        GitRepo = new GitRepo(Util.FilePath(folder, yaml["repo"]), parent.GitInstallationPath);
        VersionFacts = CreateVersionFacts(yaml["version facts"] as YamlMappingNode);
        NbtTranslations = yaml.Go("nbt translations").ToList(x => new NbtTranslationOptions((YamlMappingNode)x)) ?? new List<NbtTranslationOptions>();
    }

    protected abstract VersionFacts CreateVersionFacts(YamlMappingNode yaml);

    public void TranslateNbt(string path)
    {
        foreach (var mode in NbtTranslations)
        {
            if (mode.ShouldTranslate(path))
                mode.Translate(path);
        }
    }
}
