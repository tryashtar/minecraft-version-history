namespace MinecraftVersionHistory;

public class ListJsonSorter : PathedJsonSorter
{
    public readonly string SortBy;
    public ListJsonSorter(DateTime? required, INodeFinder finder) : base(required, finder)
    {

    }

    public override void SortSelected(JsonNode token)
    {
        if (token is JsonArray arr)
        {
            var sorted = arr.OrderBy(x => (string)x).ToList();
            arr.Clear();
            foreach (var entry in sorted) arr.Add(entry);
        }
    }
}
