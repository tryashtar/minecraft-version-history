﻿namespace MinecraftVersionHistory;

public class BedrockUpdater : Updater
{
    public BedrockUpdater(AppConfig config) : base(config)
    { }

    protected override VersionConfig VersionConfig => Config.Bedrock;

    protected override IEnumerable<Version> FindVersions()
    {
        foreach (var folder in VersionConfig.InputFolders)
        {
            foreach (var zip in Directory.EnumerateFiles(folder))
            {
                if (Path.GetExtension(zip) == ".zip")
                    yield return new BedrockVersion(zip, VersionConfig.VersionFacts);
            }
        }
    }
}
