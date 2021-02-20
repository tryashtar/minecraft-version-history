using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class JsonSorter
    {
        private readonly NodeMatcher[] Path;
        private readonly SortOperation Operation;
        public JsonSorter(YamlMappingNode node)
        {
            Path = node.Go("path").ToList(x => NodeMatcher.Create(x)).ToArray();
            Operation = ParseOperation((string)node["operation"]);
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
