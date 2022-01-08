namespace MinecraftVersionHistory;

public class OrderedJsonSorter : PathedJsonSorter
{
    public readonly string[] Order;
    public OrderedJsonSorter(DateTime? required, IEnumerable<NodeMatcher> path, IEnumerable<string> order) : base(required, path)
    {
        Order = order.ToArray();
    }

    public override void SortSelected(JToken token)
    {
        if (token is JObject obj)
            Util.SortKeys(obj, Order);
    }
}
