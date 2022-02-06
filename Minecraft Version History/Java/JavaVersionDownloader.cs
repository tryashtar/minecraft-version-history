namespace MinecraftVersionHistory;

public class JavaVersionDownloader
{
    public JavaVersionDownloader()
    {

    }

    const string LAUNCHER_MANIFEST = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    public void DownloadMissing(List<string> folders, AppConfig config)
    {
        Profiler.Start("Checking for new versions");
        var versions = JObject.Parse(Util.DownloadString(LAUNCHER_MANIFEST))["versions"];
        var commits = config.Java.GitRepo.CommittedVersions().ToList();
        foreach (var version in versions)
        {
            var name = (string)version["id"];
            var url = (string)version["url"];
            if (commits.Any(x => x.Message == name))
                continue;
            (string destination, string json_file, string jar_file) Data(string folder)
            {
                string destination = Path.Combine(folder, name);
                return (
                    destination,
                    Path.Combine(destination, name + ".json"),
                    Path.Combine(destination, name + ".jar")
                );
            }
            var all_data = folders.Select(Data).ToList();
            if (all_data.Any(x => File.Exists(x.jar_file) && File.Exists(x.jar_file)))
                continue;
            var download_location = all_data[0];
            Console.WriteLine($"Downloading new version: {name}");
            Directory.CreateDirectory(download_location.destination);
            if (!File.Exists(download_location.json_file))
                Util.DownloadFile(url, download_location.json_file);
            var client_jar = (string)JObject.Parse(File.ReadAllText(download_location.json_file))["downloads"]["client"]["url"];
            if (!File.Exists(download_location.jar_file))
                Util.DownloadFile(client_jar, download_location.jar_file);
        }
        Profiler.Stop();
    }
}
