using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public class JavaVersionDownloader
    {
        public JavaVersionDownloader()
        {

        }

        const string LAUNCHER_MANIFEST = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
        public void DownloadMissing(string folder, AppConfig config)
        {
            using var client = new WebClient();
            Console.WriteLine("Checking for new versions...");
            var versions = JObject.Parse(client.DownloadString(LAUNCHER_MANIFEST))["versions"];
            var commits = GitWrapper.CommittedVersions(config.Java.OutputRepo, config.GitInstallationPath).ToList();
            foreach (var version in versions)
            {
                var name = (string)version["id"];
                var url = (string)version["url"];
                if (commits.Any(x => x.Message == name))
                    continue;
                string destination = Path.Combine(folder, name);
                string json_file = Path.Combine(destination, name + ".json");
                string jar_file = Path.Combine(destination, name + ".jar");
                if (File.Exists(json_file) && File.Exists(jar_file))
                    continue;
                Console.WriteLine($"Downloading new version: {name}");
                Directory.CreateDirectory(destination);
                if (!File.Exists(json_file))
                    client.DownloadFile(url, json_file);
                var client_jar = (string)JObject.Parse(File.ReadAllText(json_file))["downloads"]["client"]["url"];
                if (!File.Exists(jar_file))
                    client.DownloadFile(client_jar, jar_file);
            }
        }
    }
}
