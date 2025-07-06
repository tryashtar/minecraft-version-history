namespace MinecraftVersionHistory;

public class VersionGraph
{
    private readonly VersionNode Root;
    private readonly List<ReleaseBranch> Branches = new();
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

    public IEnumerable<VersionNode> Flatten()
    {
        var stack = new Stack<VersionNode>();
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

    private class BranchSorter : IComparer<ReleaseBranch>
    {
        private readonly VersionFacts Facts;
        public BranchSorter(VersionFacts facts)
        {
            Facts = facts;
        }

        public int Compare(ReleaseBranch? x, ReleaseBranch? y)
        {
            return Facts.Compare(x.Versions.First().Version, y.Versions.First().Version);
        }
    }
}
