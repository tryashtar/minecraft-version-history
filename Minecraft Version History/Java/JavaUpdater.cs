namespace MinecraftVersionHistory;

public class JavaUpdater : Updater
{
    public JavaUpdater(AppConfig config) : base(config)
    {
    }

    protected override VersionConfig VersionConfig => Config.Java;

    protected override IEnumerable<JavaVersion> FindVersions()
    {
        foreach (var folder in VersionConfig.InputFolders.Where(x => Directory.Exists(x.Folder)))
        {
            foreach (var version in Directory.EnumerateDirectories(folder.Folder, "*",
                         folder.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                if (JavaVersion.LooksValid(version))
                    yield return new JavaVersion(version, VersionConfig.VersionFacts);
            }
        }
    }

    private const string LAUNCHER_MANIFEST = "https://piston-meta.mojang.com/mc/game/version_manifest.json";

    public void DownloadMissing(string destination_folder, AppConfig config)
    {
        Profiler.Start("Checking for new versions");
        var web_versions = (JsonArray)JsonNode.Parse(Util.DownloadString(LAUNCHER_MANIFEST))["versions"];
        var commits = config.Java.GitRepo.CommittedVersions().Select(x => x.Message).ToHashSet();
        var local_versions = FindVersions().ToDictionary(x => x.Name);
        bool found_any = false;
        foreach (var version in web_versions)
        {
            var name = (string)version["id"];
            var url = (string)version["url"];
            if (commits.Contains(name))
                continue;
            if (local_versions.TryGetValue(name, out var existing) && File.Exists(existing.JarFilePath) &&
                File.Exists(existing.LauncherJsonPath))
                continue;
            found_any = true;
            var download_location = Path.Combine(destination_folder, name);
            var json_location = Path.Combine(download_location, name + ".json");
            var jar_location = Path.Combine(download_location, name + ".jar");
            Console.WriteLine($"Downloading new version: {name}");
            Directory.CreateDirectory(download_location);
            if (!File.Exists(json_location))
                Util.DownloadFile(url, json_location);
            var client_jar =
                (string)JsonObject.Parse(File.ReadAllText(json_location))["downloads"]["client"]["url"];
            if (!File.Exists(jar_location))
                Util.DownloadFile(client_jar, jar_location);
        }

        Profiler.Stop();
        if (found_any)
            CreateGraph();
    }
}