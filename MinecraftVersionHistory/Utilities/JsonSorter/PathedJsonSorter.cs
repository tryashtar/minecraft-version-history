namespace MinecraftVersionHistory;

public abstract class PathedJsonSorter : BaseJsonSorter
{
    public readonly INodeFinder Finder;
    private readonly NodeMatcher Matches;
    public PathedJsonSorter(INodeFinder finder, NodeMatcher matches) : base()
    {
        Finder = finder;
        Matches = matches;
    }

    public override void Sort(JsonNode root)
    {
        var selected = Finder.FindNodes(root);
        foreach (var (name, node) in selected)
        {
            if (Matches == null || Matches.Matches(name, node))
                SortSelected(node);
        }
    }

    public abstract void SortSelected(JsonNode token);
}
