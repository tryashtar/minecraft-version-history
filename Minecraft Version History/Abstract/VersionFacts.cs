using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class VersionFacts
    {
        private readonly List<Regex> SkipVersions;
        private readonly List<Regex> InsaneBranches;
        private readonly List<Regex> InsaneVersions;
        private readonly Dictionary<string, string> ParentsMap;
        private readonly Dictionary<Regex, string> RegexReleases;
        private readonly List<SnapshotSpec> SnapshotReleases;
        public VersionFacts(YamlMappingNode yaml)
        {
            SkipVersions = yaml.Go("skip").ToList(x => new Regex((string)x)) ?? new List<Regex>();
            InsaneBranches = yaml.Go("insane", "releases").ToList(x => new Regex((string)x)) ?? new List<Regex>();
            InsaneVersions = yaml.Go("insane", "versions").ToList(x => new Regex((string)x)) ?? new List<Regex>();
            ParentsMap = yaml.Go("parents").ToStringDictionary() ?? new Dictionary<string, string>();
            RegexReleases = yaml.Go("releases", "regex").ToDictionary(x => new Regex((string)x), x => (string)x) ?? new Dictionary<Regex, string>();
            SnapshotReleases = yaml.Go("releases", "snapshots").ToList(x => new SnapshotSpec((YamlMappingNode)x)) ?? new List<SnapshotSpec>();
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

        public bool IsInsaneRelease(string release)
        {
            return InsaneBranches.Any(x => x.IsMatch(release));
        }

        public bool IsInsaneVersion(Version version)
        {
            return InsaneVersions.Any(x => x.IsMatch(version.Name));
        }

        public string GetReleaseName(Version version)
        {
            foreach (var candidate in RegexReleases)
            {
                if (candidate.Key.IsMatch(version.Name))
                    return candidate.Key.Replace(version.Name, candidate.Value);
            }
            if (SnapshotSpec.IsSnapshot(version, out var match))
            {
                foreach (var candidate in SnapshotReleases)
                {
                    if (candidate.Matches(match))
                        return candidate.Release;
                }
            }
            throw new KeyNotFoundException($"What release is {version} for?");
        }

        public string SpecialParent(Version version)
        {
            ParentsMap.TryGetValue(version.Name, out var result);
            return result;
        }

        private class SnapshotSpec
        {
            private static readonly Regex SnapshotRegex = new Regex(@"(?<year>\d\d)w(?<week>\d\d).");
            public readonly string Release;
            private readonly int Year;
            private readonly int FirstWeek;
            private readonly int LastWeek;
            private readonly bool HasWeeks;
            public SnapshotSpec(YamlMappingNode node)
            {
                Year = int.Parse((string)node["year"]);
                Release = (string)node["release"];
                var weeks = node.TryGet("weeks") as YamlSequenceNode;
                if (weeks == null)
                    HasWeeks = false;
                else
                {
                    HasWeeks = true;
                    FirstWeek = int.Parse((string)weeks.First());
                    LastWeek = int.Parse((string)weeks.Last());
                }
            }

            public static bool IsSnapshot(Version version, out Match match)
            {
                match = SnapshotRegex.Match(version.Name);
                return match.Success;
            }

            public bool Matches(Match match)
            {
                int year = int.Parse(match.Groups["year"].Value) + 2000;
                int week = int.Parse(match.Groups["week"].Value);
                if (year == Year)
                {
                    if (!HasWeeks)
                        return true;
                    return week >= FirstWeek && week <= LastWeek;
                }
                return false;
            }
        }
    }
}
