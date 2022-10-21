﻿namespace MinecraftVersionHistory;

public interface IJsonSorter
{
    void Sort(JsonNode root);
    bool ShouldSort(Version version);
    bool ShouldSort(string path);
}

public static class JsonSorterFactory
{
    public static IJsonSorter Create(YamlNode node)
    {
        if (node is YamlMappingNode map)
        {
            var require = node.Go("require").NullableParse(x => new SorterRequirements((YamlMappingNode)x)) ?? new SorterRequirements();
            INodeFinder finder = null;
            var path = node.Go("path");
            if (path != null)
                finder = new ForwardNodeFinder(path.ToList(NodeMatcher.Create));
            var up_path = node.Go("up_path");
            if (up_path != null)
                finder = new BackwardNodeFinder(up_path.ToList(NodeMatcher.Create));
            var select = node.Go("by").NullableParse(x => new ForwardNodeFinder(x.ToList(NodeMatcher.Create)));
            var pick = node.Go("pick").NullableStructParse(x => StringUtils.ParseUnderscoredEnum<KeyOrValue>((string)x)) ?? KeyOrValue.Auto;
            var order = node.Go("order").ToStringList();
            bool after = node.Go("after").NullableStructParse(x => Boolean.Parse(x.String())) ?? false;
            var matches = node.Go("matches").NullableParse(NodeMatcher.Create);
            return new JsonSorter(require, finder, select, pick, order, after, matches);
        }
        else if (node is YamlSequenceNode seq)
            return new MultiJsonSorter(new SorterRequirements(), seq.ToList(JsonSorterFactory.Create));
        throw new ArgumentException($"Can't turn {node} into a json sorter");
    }
}

public enum KeyOrValue
{
    Key,
    Value,
    Auto
}
