namespace MinecraftVersionHistory;

public abstract class VersionConfig
{
    public readonly List<FolderSpec> InputFolders;
    public readonly GitRepo GitRepo;
    public readonly VersionFacts VersionFacts;
    public readonly List<NbtTranslationOptions> NbtTranslations;
    public VersionConfig(string folder, AppConfig parent, YamlMappingNode yaml)
    {
        InputFolders = yaml.Go("version folders").ToList(x => ParseFolder(folder, x));
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
            {
                try
                {
                    mode.Translate(path);
                }
                catch (InvalidDataException ex)
                {
                    Console.WriteLine($"Bad NBT file {path}");
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }

    private static FolderSpec ParseFolder(string base_folder, YamlNode node)
    {
        if (node is YamlScalarNode s)
            return new(Util.FilePath(base_folder, s), false);
        return new FolderSpec(Util.FilePath(base_folder, node.Go("folder")), node.Go("recursive").Bool() ?? false);
    }
}

public record FolderSpec(string Folder, bool Recursive);
