namespace MinecraftVersionHistory;

public class KeysJsonSorter : PathedJsonSorter
{
    public KeysJsonSorter(DateTime? required, IEnumerable<NodeMatcher> path) : base(required, path)
    { }

    public override void SortSelected(JToken token)
    {
        if (token is JObject obj)
            Util.SortKeys(obj);
    }
}
