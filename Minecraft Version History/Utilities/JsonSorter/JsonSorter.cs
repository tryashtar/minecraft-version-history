namespace MinecraftVersionHistory;

public class JsonSorter : PathedJsonSorter
{
    private readonly INodeFinder[] SortBy;
    private readonly KeyOrValue Pick;
    private readonly List<string> Order;
    private readonly bool After;
    public JsonSorter(INodeFinder finder, IEnumerable<INodeFinder> sort_by, KeyOrValue pick, IEnumerable<string> order, bool after, NodeMatcher matches) : base(finder, matches)
    {
        SortBy = sort_by?.ToArray();
        Pick = pick;
        Order = order?.ToList();
        After = after;
    }

    public override void SortSelected(JsonNode token)
    {
        if (token is JsonObject obj)
        {
            IEnumerable<KeyValuePair<string, JsonNode>> tokens = new List<KeyValuePair<string, JsonNode>>(obj);
            obj.Clear();
            tokens = tokens.OrderBy(x => x, new Comparer(this));
            foreach (var item in tokens)
            {
                obj.Add(item.Key, item.Value);
            }
        }
        else if (token is JsonArray arr)
        {
            IEnumerable<JsonNode> tokens = new List<JsonNode>(arr);
            arr.Clear();
            tokens = tokens.OrderBy(x => x, new Comparer(this));
            foreach (var item in tokens)
            {
                arr.Add(item);
            }
        }
    }

    private string GetSortItem(INodeFinder? find, string name, JsonNode node)
    {
        if (find != null)
        {
            var result = find.FindNodes(node);
            if (result.Any())
                (name, node) = result.First();
        }
        if (name != null && Pick != KeyOrValue.Value)
            return name;
        if (node is JsonValue val)
            return val.ToString();
        return null;
    }

    private class Comparer : IComparer<KeyValuePair<string, JsonNode>>, IComparer<JsonNode>
    {
        private readonly JsonSorter Owner;
        public Comparer(JsonSorter owner) { Owner = owner; }

        private int Compare(string xs, string ys)
        {
            if (Owner.Order == null)
            {
                if (decimal.TryParse(xs, out decimal xn) && decimal.TryParse(ys, out decimal yn))
                    return xn.CompareTo(yn);
                return String.Compare(xs, ys);
            }
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
            if (Owner.SortBy == null)
            {
                string xs = Owner.GetSortItem(null, x.Key, x.Value);
                string ys = Owner.GetSortItem(null, y.Key, y.Value);
                return Compare(xs, ys);
            }
            foreach (var item in Owner.SortBy)
            {
                string xs = Owner.GetSortItem(item, x.Key, x.Value);
                string ys = Owner.GetSortItem(item, y.Key, y.Value);
                int result = Compare(xs, ys);
                if (result != 0)
                    return result;
            }
            return 0;
        }

        public int Compare(JsonNode? x, JsonNode? y)
        {
            if (Owner.SortBy == null)
            {
                string xs = Owner.GetSortItem(null, null, x);
                string ys = Owner.GetSortItem(null, null, y);
                return Compare(xs, ys);
            }
            foreach (var item in Owner.SortBy)
            {
                string xs = Owner.GetSortItem(item, null, x);
                string ys = Owner.GetSortItem(item, null, y);
                int result = Compare(xs, ys);
                if (result != 0)
                    return result;
            }
            return 0;
        }
    }
}
