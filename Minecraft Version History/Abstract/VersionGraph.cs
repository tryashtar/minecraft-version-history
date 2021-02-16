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
        private readonly Dictionary<string, VersionNode> BranchTips = new Dictionary<string, VersionNode>();
        public VersionGraph()
        {

        }

        public void Add(Version version, string release)
        {
            var node = new VersionNode(version, release);
            if (Root == null)
            {
                Root = node;
            }
            else if (version.ReleaseTime < Root.Version.ReleaseTime)
            {
                Root.SetParent(node);
                Root = node;
            }
            else
            {
                if (BranchTips.TryGetValue(release, out var latest))
                {
                    node.SetParent(latest);
                }
                else
                {
                    node.SetParent(Root);
                }
                BranchTips[release] = node;
            }
        }

        public override string ToString()
        {
            return String.Join("\n", Root.ToStringRecursive());
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

            public IEnumerable<string> ToStringRecursive(int indents = 0)
            {
                yield return new string(' ', indents) + Version.ToString() + " (" + ReleaseName + ")";
                int next_indents = ChildNodes.Count > 1 ? indents + 1 : indents;
                foreach (var child in ChildNodes)
                {
                    foreach (var item in child.ToStringRecursive(next_indents))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
