namespace MinecraftVersionHistory;

public class ByKeyJsonSorter : PathedJsonSorter
{
    public readonly INodeFinder SortBy;
    public ByKeyJsonSorter(DateTime? required, INodeFinder finder, INodeFinder sort_by) : base(required, finder)
    {
        SortBy = sort_by;
    }

    public override void SortSelected(JsonNode token)
    {
        if (token is JsonArray arr)
        {
            var sorted = arr.OrderBy(GetSortName).ToList();
            arr.Clear();
            foreach (var entry in sorted) arr.Add(entry);
        }
    }

    private string GetSortName(JsonNode t)
    {
        return SortBy.FindNodes(t).First().name;
    }
}
