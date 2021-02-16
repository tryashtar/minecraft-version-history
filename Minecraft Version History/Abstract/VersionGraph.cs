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
        private VersionNode Root;
        private readonly Dictionary<string, ReleaseBranch> Branches = new Dictionary<string, ReleaseBranch>();
        private readonly Config Config;
        public VersionGraph(Config config)
        {
            Config = config;
        }

        private void OrderBranches()
        {
            var branches = Branches.Values.OrderBy(x => x.Earliest);
            foreach (var branch in branches)
            {

            }
        }

        public void Add(Version version, string release)
        {
            var node = new VersionNode(version, release);
            if (Branches.TryGetValue(release, out var branch))
            {
                branch.InsertVersion(node);
            }
            else
            {
                Branches[release] = new ReleaseBranch(this, release);
                Branches[release].InsertVersion(node);
            }
            OrderBranches();
        }

        public override string ToString()
        {
            return String.Join("\n", Root.ToStringRecursive());
        }

        private class ReleaseBranch
        {
            private readonly VersionGraph Graph;
            private readonly List<VersionNode> VersionList = new List<VersionNode>();
            public ReadOnlyCollection<VersionNode> Versions => VersionList.AsReadOnly();
            public readonly string Name;
            public DateTime Earliest = DateTime.MaxValue;
            public DateTime Latest = DateTime.MinValue;
            public ReleaseBranch(VersionGraph graph, string name)
            {
                Graph = graph;
                Name = name;
            }

            public void InsertVersion(VersionNode node)
            {
                if (node.Version.ReleaseTime < Earliest)
                    Earliest = node.Version.ReleaseTime;
                if (node.Version.ReleaseTime > Latest)
                    Latest = node.Version.ReleaseTime;
                VersionNode before = null;
                var after = Enumerable.Empty<VersionNode>();
                if (VersionList.Any())
                {
                    before = VersionList.First().Parent;
                    after = VersionList.Last().Children;
                }
                VersionList.Add(node);
                VersionList.Sort(BranchVersionSorter.Instance);
            }
        }

        private class BranchVersionSorter : IComparer<VersionNode>
        {
            private BranchVersionSorter() { }
            public static BranchVersionSorter Instance = new BranchVersionSorter();

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

            public IEnumerable<string> ToStringRecursive() => ToStringRecursive("", true);

            private IEnumerable<string> ToStringRecursive(string indent, bool last)
            {
                var builder = new StringBuilder();
                builder.Append(indent);
                if (last)
                {
                    builder.Append("└");
                    if (ChildNodes.Count > 1)
                        indent += "  ";
                }
                else
                {
                    builder.Append("|-");
                    if (ChildNodes.Count > 1)
                        indent += "| ";
                }
                builder.Append($"{Version} ({ReleaseName})");
                yield return builder.ToString();

                for (int i = 0; i < ChildNodes.Count; i++)
                {
                    foreach (var item in ChildNodes[i].ToStringRecursive(indent, i == ChildNodes.Count - 1))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
