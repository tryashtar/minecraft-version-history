namespace MinecraftVersionHistory;

public abstract class PathedJsonSorter : BaseJsonSorter
{
    public readonly INodeFinder Finder;
    private readonly NodeMatcher Matches;
    public PathedJsonSorter(SorterRequirements required, INodeFinder finder, NodeMatcher matches) : base(required)
    {
        Finder = finder;
        Matches = matches;
    }

    public override void Sort(JsonObject root)
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
