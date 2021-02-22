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
    public class VersionFacts : IComparer<Version>
    {
        private readonly List<Regex> SkipVersions;
        private readonly List<Regex> InsaneBranches;
        private readonly List<Regex> InsaneVersions;
        private readonly Dictionary<string, string> ParentsMap;
        private readonly Dictionary<Regex, string> RegexReleases;
        private readonly List<SnapshotSpec> SnapshotReleases;
        private readonly List<string> VersionOrder;
        public VersionFacts(YamlMappingNode yaml)
        {
            SkipVersions = yaml.Go("skip").ToList(x => new Regex((string)x)) ?? new List<Regex>();
            InsaneBranches = yaml.Go("insane", "releases").ToList(x => new Regex((string)x)) ?? new List<Regex>();
            InsaneVersions = yaml.Go("insane", "versions").ToList(x => new Regex((string)x)) ?? new List<Regex>();
            ParentsMap = yaml.Go("parents").ToStringDictionary() ?? new Dictionary<string, string>();
            RegexReleases = yaml.Go("releases", "regex").ToDictionary(x => new Regex((string)x), x => (string)x) ?? new Dictionary<Regex, string>();
            SnapshotReleases = yaml.Go("releases", "snapshots").ToList(x => new SnapshotSpec((YamlMappingNode)x)) ?? new List<SnapshotSpec>();
            VersionOrder = yaml.Go("ordering", "versions").ToStringList() ?? new List<string>();
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

        public int Compare(Version x, Version y)
        {
            int x_index = VersionOrder.IndexOf(x.Name);
            int y_index = VersionOrder.IndexOf(y.Name);
            if (x_index != -1 && y_index != -1)
                return x_index.CompareTo(y_index);
            return x.ReleaseTime.CompareTo(y.ReleaseTime);
        }
    }
}
