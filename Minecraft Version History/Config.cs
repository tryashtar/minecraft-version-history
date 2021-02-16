using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

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
        public JavaConfig(YamlMappingNode yaml)
        {
            VersionFacts = new VersionFacts(yaml["version facts"] as YamlMappingNode);
            InputFolder = (string)yaml["version folder"];
            OutputRepo = (string)yaml["repo"];
            JavaInstallationPath = (string)yaml["java install"];
            FernflowerPath = (string)yaml["fernflower jar"];
            CfrPath = (string)yaml["cfr jar"];
            SpecialSourcePath = (string)yaml["special source jar"];
            ServerJarFolder = (string)yaml["server jars"];
            Decompiler = ParseDecompiler((string)yaml["decompiler"]);
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
        public BedrockConfig(YamlMappingNode yaml)
        {
            VersionFacts = new VersionFacts(yaml["version facts"] as YamlMappingNode);
        }
    }

    public class VersionFacts
    {
        private readonly List<Regex> SkipVersions;
        private readonly Dictionary<string, string> ParentsMap;
        private readonly Dictionary<Regex, string> ReleasesMap;
        public VersionFacts(YamlMappingNode yaml)
        {
            SkipVersions = yaml["skip"].ToList(x => new Regex((string)x));
            ParentsMap = yaml["parents"].ToStringDictionary();
            ReleasesMap = yaml["releases"].ToDictionary(x => new Regex((string)x), x => (string)x);
        }

        public bool ShouldSkip(IVersionInfo version)
        {
            foreach (var item in SkipVersions)
            {
                if (item.IsMatch(version.VersionName))
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
