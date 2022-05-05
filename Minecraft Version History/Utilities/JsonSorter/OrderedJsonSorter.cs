namespace MinecraftVersionHistory;

public class OrderedJsonSorter : PathedJsonSorter
{
    public readonly string[] Order;
    public OrderedJsonSorter(DateTime? required, INodeFinder finder, IEnumerable<string> order) : base(required, finder)
    {
        Order = order.ToArray();
    }

    public override void SortSelected(JsonNode token)
    {
        if (token is JsonObject obj)
            Util.SortKeys(obj, Order);
    }
}
