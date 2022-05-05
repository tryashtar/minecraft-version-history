namespace MinecraftVersionHistory;

public interface IJsonSorter
{
    void Sort(JsonObject root);
    bool ShouldSort(Version version);
}

public static class JsonSorterFactory
{
    public static IJsonSorter Create(YamlNode node)
    {
        if (node is YamlMappingNode map)
        {
            DateTime? required = null;
            var after = node.Go("after").String();
            if (after != null)
                required = DateTime.Parse(after);
            var multi = node.Go("multi").ToList(JsonSorterFactory.Create);
            if (multi != null)
                return new MultiJsonSorter(required, multi);
            INodeFinder finder = null;
            var path = node.Go("path");
            if (path != null)
                finder = new ForwardNodeFinder(path.ToList(x => NodeMatcher.Create(x)));
            var up_path = node.Go("up_path");
            if (up_path != null)
                finder = new BackwardNodeFinder(up_path.ToList(x => NodeMatcher.Create(x)));
            var op = StringUtils.ParseUnderscoredEnum<SortOperation>((string)node["operation"]);
            if (op == SortOperation.SortKeys)
                return new KeysJsonSorter(required, finder);
            else if (op == SortOperation.Sort)
                return new ListJsonSorter(required, finder);
            else if (op == SortOperation.SortBy)
            {
                var sort = node.Go("by");
                return new ByKeyJsonSorter(required, finder, new ForwardNodeFinder(sort.ToList(x => NodeMatcher.Create(x))));
            }
            else if (op == SortOperation.Order)
            {
                var order = node.Go("order").ToStringList();
                return new OrderedJsonSorter(required, finder, order);
            }
        }
        throw new ArgumentException($"Can't turn {node} into a json sorter");
    }
}

public enum SortOperation
{
    SortKeys,
    Sort,
    SortBy,
    Order
}
