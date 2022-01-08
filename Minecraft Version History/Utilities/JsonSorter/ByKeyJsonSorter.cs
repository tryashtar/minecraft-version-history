namespace MinecraftVersionHistory;

public class ByKeyJsonSorter : PathedJsonSorter
{
    public readonly string SortBy;
    public ByKeyJsonSorter(DateTime? required, IEnumerable<NodeMatcher> path, string sort_by) : base(required, path)
    {
        SortBy = sort_by;
    }

    public override void SortSelected(JToken token)
    {
        if (token is JArray arr)
        {
            var sorted = arr.OrderBy(x => x[SortBy]).ToList();
            arr.Clear();
            foreach (var entry in sorted) arr.Add(entry);
        }
    }
}
