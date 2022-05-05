namespace MinecraftVersionHistory;

public abstract class PathedJsonSorter : BaseJsonSorter
{
    public readonly INodeFinder Finder;
    public PathedJsonSorter(SorterRequirements required, INodeFinder finder) : base(required)
    {
        Finder = finder;
    }

    public override void Sort(JsonObject root)
    {
        var selected = Finder.FindNodes(root);
        foreach (var item in selected)
        {
            SortSelected(item.node);
        }
    }

    public abstract void SortSelected(JsonNode token);
}
