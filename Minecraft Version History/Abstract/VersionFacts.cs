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

        public bool ShouldSkip(Version version)
        {
            foreach (var item in SkipVersions)
            {
                if (item.IsMatch(version.Name))
                    return true;
            }
            return false;
        }

        public string GetReleaseName(Version version)
        {
            foreach (var item in ReleasesMap)
            {
                if (item.Key.IsMatch(version.Name))
                    return item.Value;
            }
            throw new ArgumentException($"What release is {version} for?");
        }
    }
}
