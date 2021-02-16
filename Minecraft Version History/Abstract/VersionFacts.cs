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
        private readonly List<(string name, List<Regex> matches)> Releases;
        public VersionFacts(YamlMappingNode yaml)
        {
            SkipVersions = yaml["skip"].ToList(x => new Regex((string)x));
            ParentsMap = yaml["parents"].ToStringDictionary();
            Releases = yaml["releases"].ToList(x => ParseReleaseEntry((YamlMappingNode)x));
        }

        private (string name, List<Regex> matches) ParseReleaseEntry(YamlMappingNode node)
        {
            var single = node.Children.Single();
            List<Regex> matches;
            if (single.Value is YamlSequenceNode sequence)
                matches = sequence.ToList(x => new Regex((string)x));
            else
                matches = new List<Regex> { new Regex((string)single.Value) };
            return ((string)single.Key, matches);
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
            foreach (var candidate in Releases)
            {
                if (candidate.matches.Any(x => x.IsMatch(version.Name)))
                    return candidate.name;
            }
            return "UNKNOWN";
            throw new ArgumentException($"What release is {version} for?");
        }
    }
}
