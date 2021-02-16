using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public interface IVersionInfo
    {
        string VersionName { get; }
        DateTime ReleaseTime { get; }
    }

    public class JavaVersionInfo : IVersionInfo
    {
        public string VersionName { get; private set; }
        public DateTime ReleaseTime { get; private set; }
        public readonly string JarPath;
        public readonly string ServerJarURL;
        public readonly string MappingsURL;
        public JavaVersionInfo(string folder)
        {
            VersionName = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, VersionName + ".json");
            JarPath = Path.Combine(folder, VersionName + ".jar");
            var json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            ServerJarURL = (string)json["downloads"]?["server"]?["url"];
            MappingsURL = (string)json["downloads"]?["client_mappings"]?["url"];
        }
    }

    public class BedrockVersionInfo : IVersionInfo
    {
        public string VersionName { get; private set; }
        public DateTime ReleaseTime { get; private set; }
        public readonly string ZipPath;
        public BedrockVersionInfo(string zip_path)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zip_path))
            {
                ZipPath = zip_path;
                var mainappx = GetMainAppx(zip);
                VersionName = Path.GetFileName(mainappx.FullName).Split('_')[1];
                ReleaseTime = zip.Entries[0].LastWriteTime.UtcDateTime;
            }
        }

        private ZipArchiveEntry GetMainAppx(ZipArchive zip)
        {
            foreach (var entry in zip.Entries)
            {
                string filename = Path.GetFileName(entry.FullName);
                // example: Minecraft.Windows_1.1.0.0_x64_UAP.Release.appx
                if (filename.StartsWith("Minecraft.Windows") && Path.GetExtension(filename) == ".appx")
                    return entry;
            }
            throw new FileNotFoundException($"Could not find main APPX");
        }
    }
}
