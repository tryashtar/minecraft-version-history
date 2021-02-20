using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class VersionGraph
    {
        private readonly VersionNode Root;
        private readonly List<ReleaseBranch> Branches = new List<ReleaseBranch>();
        private readonly Config Config;
        public VersionGraph(Config config, IEnumerable<Version> versions)
        {
            Config = config;
            var releases = versions.GroupBy(x => config.VersionFacts.GetReleaseName(x));
            foreach (var branch in releases)
            {
                Branches.Add(new ReleaseBranch(this, branch.Key, branch));
            }
            var sorter = new BranchSorter();
            Branches.Sort(sorter);
            Root = Branches.First().Versions.First();
            for (int i = Branches.Count - 1; i >= 1; i--)
            {
                var start = Branches[i].Versions.First();
                var sane_parent = Branches.Take(i).Last(x => !config.VersionFacts.IsInsaneRelease(x.Name)).Versions.Last(x => x.Version.ReleaseTime <= start.Version.ReleaseTime);
                start.SetParent(sane_parent);
            }
            foreach (var version in versions)
            {
                string special = config.VersionFacts.SpecialParent(version);
                if (special != null)
                {
                    var found = versions.FirstOrDefault(x => x.Name == special);
                    if (found != null)
                    {
                        var node1 = FindNode(version);
                        var node2 = FindNode(found);
                        node1.SetParent(node2);
                    }
                }
            }
        }

        private VersionNode FindNode(Version version)
        {
            var release = Config.VersionFacts.GetReleaseName(version);
            var branch = Branches.First(x => x.Name == release);
            return branch.Versions.First(x => x.Version == version);
        }

        public override string ToString()
        {
            return String.Join("\n", Root.ToStringRecursive());
        }

        private class ReleaseBranch
        {
            private readonly VersionGraph Graph;
            private readonly List<VersionNode> VersionList;
            public ReadOnlyCollection<VersionNode> Versions => VersionList.AsReadOnly();
            public readonly string Name;
            public ReleaseBranch(VersionGraph graph, string name, IEnumerable<Version> versions)
            {
                Graph = graph;
                Name = name;
                var sorter = new BranchVersionSorter();
                VersionList = versions.Select(x => new VersionNode(x, name)).OrderBy(x => x, sorter).ToList();
                for (int i = VersionList.Count - 1; i >= 1; i--)
                {
                    VersionList[i].SetParent(VersionList[i - 1]);
                }
            }
        }

        private class BranchSorter : IComparer<ReleaseBranch>
        {
            public int Compare(ReleaseBranch x, ReleaseBranch y)
            {
                return x.Versions.First().Version.ReleaseTime.CompareTo(y.Versions.First().Version.ReleaseTime);
            }
        }

        private class BranchVersionSorter : IComparer<VersionNode>
        {
            public int Compare(VersionNode x, VersionNode y)
            {
                return x.Version.ReleaseTime.CompareTo(y.Version.ReleaseTime);
            }
        }

        private class VersionNode
        {
            public readonly Version Version;
            public readonly string ReleaseName;
            public VersionNode Parent { get; private set; }
            public ReadOnlyCollection<VersionNode> Children => ChildNodes.AsReadOnly();
            private readonly List<VersionNode> ChildNodes = new List<VersionNode>();
            public VersionNode(Version version, string release)
            {
                Version = version;
                ReleaseName = release;
            }

            public void SetParent(VersionNode other)
            {
                if (Parent != null)
                    Parent.ChildNodes.Remove(this);
                Parent = other;
                if (Parent != null)
                    Parent.ChildNodes.Add(this);
            }

            public void AddChild(VersionNode other)
            {
                other.SetParent(this);
            }

            public IEnumerable<string> ToStringRecursive() => ToStringRecursive("");

            private IEnumerable<string> ToStringRecursive(string prefix)
            {
                string pointer = ChildNodes.Any() ? "│" : "└";
                yield return $"{prefix} {pointer} {Version} ({ReleaseName})";
                var paths = new List<List<string>>();
                for (int i = 0; i < ChildNodes.Count; i++)
                {
                    var rest = ChildNodes[i].ToStringRecursive(prefix);
                    paths.Add(rest.ToList());
                }
                var sorted = paths.OrderBy(x => x.Count).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    string extra = sorted.Count > 1 ? String.Concat(Enumerable.Repeat(" │", sorted.Count - i - 1)) : "";
                    foreach (var item in sorted[i]) yield return extra + item;
                }
            }
        }
    }
}
