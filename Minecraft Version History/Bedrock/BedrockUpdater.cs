namespace MinecraftVersionHistory;

public class BedrockUpdater : Updater
{
    public BedrockUpdater(AppConfig config) : base(config)
    {
    }

    protected override VersionConfig VersionConfig => Config.Bedrock;

    protected override IEnumerable<BedrockVersion> FindVersions()
    {
        foreach (var folder in VersionConfig.InputFolders.Where(x => Directory.Exists(x.Folder)))
        {
            foreach (var zip in Directory.EnumerateFiles(folder.Folder, "*",
                         folder.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                string ext = Path.GetExtension(zip);
                if (ext == ".zip")
                    yield return new BedrockVersion(zip, VersionConfig.VersionFacts, true);
                else if (ext == ".appx")
                    yield return new BedrockVersion(zip, VersionConfig.VersionFacts, false);
            }
        }
    }
}