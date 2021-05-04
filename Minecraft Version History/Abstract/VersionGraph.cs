using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public class VersionGraph
    {
        private readonly VersionNode Root;
        private readonly List<ReleaseBranch> Branches = new List<ReleaseBranch>();
        private readonly VersionFacts Facts;
        public VersionGraph(VersionFacts facts, IEnumerable<Version> versions)
        {
            Facts = facts;
            versions = versions.Where(x => !facts.ShouldSkip(x));
            var releases = versions.GroupBy(x => Facts.GetReleaseName(x));
            foreach (var branch in releases)
            {
                Branches.Add(new ReleaseBranch(facts, branch.Key, branch));
            }
            var sorter = new BranchSorter(facts);
            Branches.Sort(sorter);
            Root = Branches.First().Versions.First();
            for (int i = Branches.Count - 1; i >= 1; i--)
            {
                // set cross-branch parents with educated guesses
                // pick the last version in the previous branch that's older than the first version in this branch
                // skip "insane" branches (like april fools versions)
                var start = Branches[i].Versions.First();
                var sane_parent = Branches.Take(i).Last(x => !Facts.IsInsaneRelease(x.Name)).Versions
                    .Last(x => !Facts.IsInsaneVersion(x.Version) && Facts.Compare(start.Version, x.Version) > 0);
                start.SetParent(sane_parent);
            }
            foreach (var version in versions)
            {
                string special = Facts.SpecialParent(version);
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

        public IEnumerable<IVersionNode> Flatten()
        {
            var stack = new Stack<IVersionNode>();
            stack.Push(Root);
            while (stack.Any())
            {
                var node = stack.Pop();
                yield return node;
                foreach (var child in node.OrderedChildren().Reverse())
                {
                    stack.Push(child);
                }
            }
        }

        private VersionNode FindNode(Version version)
        {
            var release = Facts.GetReleaseName(version);
            var branch = Branches.First(x => x.Name == release);
            return branch.Versions.First(x => x.Version == version);
        }

        public override string ToString()
        {
            return String.Join("\n", Root.ToStringRecursive());
        }

        private class ReleaseBranch
        {
            private readonly List<VersionNode> VersionList;
            public ReadOnlyCollection<VersionNode> Versions => VersionList.AsReadOnly();
            public readonly string Name;
            public ReleaseBranch(VersionFacts facts, string name, IEnumerable<Version> versions)
            {
                Name = name;
                VersionList = versions.Select(x => new VersionNode(x, name)).OrderBy(x => x.Version, facts).ToList();
                for (int i = 0; i < VersionList.Count - 1; i++)
                {
                    if (facts.Compare(VersionList[i].Version, VersionList[i + 1].Version) == 0)
                        throw new ArgumentException($"Can't disambiguate order of {VersionList[i].Version} and {VersionList[i + 1].Version}");
                }
                for (int i = VersionList.Count - 1; i >= 1; i--)
                {
                    VersionList[i].SetParent(VersionList[i - 1]);
                }
            }
        }

        private class BranchSorter : IComparer<ReleaseBranch>
        {
            private readonly VersionFacts Facts;
            public BranchSorter(VersionFacts facts)
            {
                Facts = facts;
            }

            public int Compare(ReleaseBranch x, ReleaseBranch y)
            {
                return Facts.Compare(x.Versions.First().Version, y.Versions.First().Version);
            }
        }

        private class VersionNode : IVersionNode
        {
            public readonly Version Version;
            public readonly string ReleaseName;
            public VersionNode Parent { get; private set; }
            public ReadOnlyCollection<VersionNode> Children => ChildNodes.AsReadOnly();

            IVersionNode IVersionNode.Parent => Parent;
            Version IVersionNode.Version => Version;
            string IVersionNode.ReleaseName => ReleaseName;
            IEnumerable<IVersionNode> IVersionNode.Children => Children;

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
                foreach (VersionNode item in this.OrderedChildren())
                {
                    var rest = item.ToStringRecursive(prefix);
                    paths.Add(rest.ToList());
                }
                for (int i = 0; i < paths.Count; i++)
                {
                    string extra = paths.Count > 1 ? String.Concat(Enumerable.Repeat(" │", paths.Count - i - 1)) : "";
                    foreach (var item in paths[i]) yield return extra + item;
                }
            }
        }
    }
}
