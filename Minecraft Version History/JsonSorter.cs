using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class JsonSorter
    {
        private readonly NodeMatcher[] Path;
        private readonly SortOperation Operation;
        private readonly string SortBy;
        public JsonSorter(YamlMappingNode node)
        {
            Path = node.Go("path").ToList(x => NodeMatcher.Create(x)).ToArray();
            Operation = ParseOperation((string)node["operation"]);
            var by = node.TryGet("by");
            if (by != null)
                SortBy = (string)by;
        }

        public void Sort(JObject root)
        {
            IEnumerable<JToken> selected = new[] { root };
            foreach (var matcher in Path)
            {
                selected = matcher.Follow(selected);
            }
            if (Operation == SortOperation.SortKeys)
            {
                foreach (var item in selected)
                {
                    if (item is JObject obj)
                        Util.SortKeys(obj);
                }
            }
            else if (Operation == SortOperation.SortBy)
            {
                foreach (var item in selected)
                {
                    if (item is JArray arr)
                    {
                        var sorted = arr.OrderBy(x => x[SortBy]).ToList();
                        arr.Clear();
                        foreach (var entry in sorted) arr.Add(entry);
                    }
                }
            }
        }

        private static SortOperation ParseOperation(string input)
        {
            if (String.Equals(input, "sort_keys", StringComparison.OrdinalIgnoreCase))
                return SortOperation.SortKeys;
            if (String.Equals(input, "sort_by", StringComparison.OrdinalIgnoreCase))
                return SortOperation.SortBy;
            throw new ArgumentException(input);
        }

        private enum SortOperation
        {
            SortKeys,
            SortBy
        }
    }
}
