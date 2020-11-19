using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class JavaConfig
    {
        public readonly VersionFacts VersionFacts;
        public readonly string InputFolder;
        public readonly string OutputRepo;
        public readonly string JavaInstallationPath;
        public readonly string FernflowerPath;
        public readonly string CfrPath;
        public readonly string SpecialSourcePath;
        public readonly string ServerJarFolder;
        public readonly DecompilerType? Decompiler;
        public JavaConfig(JObject json)
        {
            VersionFacts = new VersionFacts(Util.PathToObject(json, "version_facts"));
            InputFolder = json["version_folder"]?.ToString();
            OutputRepo = json["repo"]?.ToString();
            JavaInstallationPath = json["java_install"]?.ToString();
            FernflowerPath = json["fernflower_jar"]?.ToString();
            CfrPath = json["cfr_jar"].ToString();
            SpecialSourcePath = json["special_source_jar"].ToString();
            ServerJarFolder = json["server_jars"].ToString();
            Decompiler = ParseDecompiler(json["decompiler"].ToString());
        }

        private static DecompilerType? ParseDecompiler(string input)
        {
            if (String.Equals(input, "fernflower", StringComparison.OrdinalIgnoreCase))
                return DecompilerType.Fernflower;
            if (String.Equals(input, "cfr", StringComparison.OrdinalIgnoreCase))
                return DecompilerType.Cfr;
            return null;
        }
    }

    public enum DecompilerType
    {
        Fernflower,
        Cfr
    }

    public class BedrockConfig
    {
        public readonly VersionFacts VersionFacts;
        public BedrockConfig(JObject json)
        {
            VersionFacts = new VersionFacts(Util.PathToObject(json, "version_facts"));
        }
    }

    public class VersionFacts
    {
        private readonly List<Regex> SkipVersions;
        private readonly List<Regex> SkipParents;
        private readonly Dictionary<string, string> ParentsMap;
        private readonly Dictionary<Regex, string> ReleasesMap;
        public VersionFacts(JObject json)
        {
            SkipVersions = Util.PathToArray(json, "skip").Select(x => new Regex(x.ToString())).ToList();
            SkipParents = Util.PathToArray(json, "parents", "skip").Select(x => new Regex(x.ToString())).ToList();
            ParentsMap = Util.PathToObject(json, "parents", "map").ToObject<Dictionary<string, string>>();
            ReleasesMap = Util.PathToObject(json, "parents", "map").ToObject<Dictionary<string, string>>().ToDictionary(kv => new Regex(kv.Key), kv => kv.Value);
        }

        public bool ShouldSkip(Version version)
        {
            foreach (var item in SkipVersions)
            {
                if (item.IsMatch(version.Name))
                    return true;
            }
            return false;
        }

        public string GetSpecialParent(Version version)
        {
            if (ParentsMap.TryGetValue(version.Name, out var result))
                return result;
            return null;
        }
    }
}
