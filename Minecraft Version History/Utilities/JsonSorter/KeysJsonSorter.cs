namespace MinecraftVersionHistory;

public class KeysJsonSorter : PathedJsonSorter
{
    public KeysJsonSorter(DateTime? required, INodeFinder finder) : base(required, finder)
    { }

    public override void SortSelected(JsonNode token)
    {
        if (token is JsonObject obj)
            Util.SortKeys(obj);
    }
}
