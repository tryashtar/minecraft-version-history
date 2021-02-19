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
                Branches[i].Versions.First().SetParent(Branches[i - 1].Versions.Last());
            }
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
