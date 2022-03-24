namespace MinecraftVersionHistory;

public class ListJsonSorter : PathedJsonSorter
{
    public readonly string SortBy;
    public ListJsonSorter(DateTime? required, IEnumerable<NodeMatcher> path) : base(required, path)
    {

    }

    public override void SortSelected(JToken token)
    {
        if (token is JArray arr)
        {
            var sorted = arr.OrderBy(x => (string)x).ToList();
            arr.Clear();
            foreach (var entry in sorted) arr.Add(entry);
        }
    }
}
