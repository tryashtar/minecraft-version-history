using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MinecraftVersionHistory
{
    public class VersionNode
    {
        public readonly Version Version;
        public readonly string ReleaseName;
        public VersionNode Parent { get; private set; }
        public ReadOnlyCollection<VersionNode> Children => ChildNodes.AsReadOnly();

        private readonly List<VersionNode> ChildNodes = new();
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
