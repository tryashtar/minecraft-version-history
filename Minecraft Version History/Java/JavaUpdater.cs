namespace MinecraftVersionHistory;

public class JavaUpdater : Updater
{
    public JavaUpdater(AppConfig config) : base(config)
    { }

    protected override VersionConfig VersionConfig => Config.Java;

    protected override IEnumerable<Version> FindVersions()
    {
        foreach (var folder in VersionConfig.InputFolders)
        {
            foreach (var version in Directory.EnumerateDirectories(folder))
            {
                if (JavaVersion.LooksValid(version))
                    yield return new JavaVersion(version);
            }
        }
    }
}
