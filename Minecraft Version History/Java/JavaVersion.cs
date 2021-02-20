using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Minecraft_Version_History
{
    public class JavaVersion : Version
    {
        public readonly string JarPath;
        public readonly string ServerJarURL;
        public readonly string MappingsURL;
        public JavaVersion(string folder)
        {
            Name = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, Name + ".json");
            JarPath = Path.Combine(folder, Name + ".jar");
            var json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            ServerJarURL = (string)json["downloads"]?["server"]?["url"];
            MappingsURL = (string)json["downloads"]?["client_mappings"]?["url"];
        }

        public override void ExtractData(string folder, Config config)
        {
            throw new NotImplementedException();
        }
    }
}
