namespace MinecraftVersionHistory;

public interface IJsonSorter
{
    void Sort(JObject root);
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
            var path = node.Go("path").ToList(x => NodeMatcher.Create(x));
            var op = StringUtils.ParseUnderscoredEnum<SortOperation>((string)node["operation"]);
            if (op == SortOperation.SortKeys)
                return new KeysJsonSorter(required, path);
            else if (op == SortOperation.SortBy)
            {
                var sort = node.Go("by").String();
                return new ByKeyJsonSorter(required, path, sort);
            }
            else if (op == SortOperation.Order)
            {
                var order = node.Go("order").ToStringList();
                return new OrderedJsonSorter(required, path, order);
            }
        }
        throw new ArgumentException($"Can't turn {node} into a json sorter");
    }
}

public enum SortOperation
{
    SortKeys,
    SortBy,
    Order
}
