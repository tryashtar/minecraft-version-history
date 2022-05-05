namespace MinecraftVersionHistory;

public class JsonSorter : PathedJsonSorter
{
    private readonly INodeFinder SortBy;
    private readonly KeyOrValue Pick;
    private readonly List<string> Order;
    private readonly bool After;
    public JsonSorter(SorterRequirements required, INodeFinder finder, INodeFinder sort_by, KeyOrValue pick, IEnumerable<string> order, bool after) : base(required, finder)
    {
        SortBy = sort_by;
        Pick = pick;
        Order = order?.ToList();
        After = after;
    }

    public override void SortSelected(JsonNode token)
    {
        if (token is JsonObject obj)
        {
            var tokens = new List<KeyValuePair<string, JsonNode>>(obj);
            obj.Clear();
            tokens.Sort(new Comparer(this));
            foreach (var item in tokens)
            {
                obj.Add(item.Key, item.Value);
            }
        }
        else if (token is JsonArray arr)
        {
            var tokens = new List<JsonNode>(arr);
            arr.Clear();
            tokens.Sort(new Comparer(this));
            foreach (var item in tokens)
            {
                arr.Add(item);
            }
        }
    }

    private string GetSortItem(string name, JsonNode node)
    {
        if (SortBy != null)
            (name, node) = SortBy.FindNodes(node).First();
        if (name != null && Pick != KeyOrValue.Value)
            return name;
        if (node is JsonValue val)
            return (string)val;
        return null;
    }

    private class Comparer : IComparer<KeyValuePair<string, JsonNode>>, IComparer<JsonNode>
    {
        private readonly JsonSorter Owner;
        public Comparer(JsonSorter owner) { Owner = owner; }

        private int Compare(string xs, string ys)
        {
            if (Owner.Order == null)
                return xs.CompareTo(ys);
            int xi = Owner.Order.IndexOf(xs);
            int yi = Owner.Order.IndexOf(ys);
            if (xi == -1)
                xi = Owner.After ? int.MinValue : int.MaxValue;
            if (yi == -1)
                yi = Owner.After ? int.MinValue : int.MaxValue;
            return xi.CompareTo(yi);
        }

        public int Compare(KeyValuePair<string, JsonNode> x, KeyValuePair<string, JsonNode> y)
        {
            string xs = Owner.GetSortItem(x.Key, x.Value);
            string ys = Owner.GetSortItem(y.Key, y.Value);
            return Compare(xs, ys);
        }

        public int Compare(JsonNode x, JsonNode y)
        {
            string xs = Owner.GetSortItem(null, x);
            string ys = Owner.GetSortItem(null, y);
            return Compare(xs, ys);
        }
    }
}
