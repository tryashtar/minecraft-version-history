using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class MergingSpec
    {
        private readonly string[] Path;
        private readonly List<string> OverwriteKeys;
        public MergingSpec(YamlMappingNode node)
        {
            Path = Util.Split((string)node["path"]);
            OverwriteKeys = node.Go("overwrite").ToStringList() ?? new List<string>();
        }

        public bool Matches(string path)
        {
            var split = Util.Split(path);
            if (split.Length != Path.Length)
                return false;
            for (int i = 0; i < split.Length; i++)
            {
                if (split[i] != Path[i])
                    return false;
            }
            return true;
        }

        public void Merge(JToken current, JToken newer)
        {
            if (current is JObject cj && newer is JObject nj)
                MergeObjects(cj, nj);
            else if (current is JArray ca && newer is JArray na)
                MergeArrays(ca, na);
        }

        public void MergeObjects(JObject current, JObject newer)
        {
            foreach (var item in newer)
            {
                bool exists = current.TryGetValue(item.Key, out var existing);
                if (!exists || OverwriteKeys.Contains(item.Key))
                    current[item.Key] = item.Value;
                else
                {
                    if (item.Value is JObject || item.Value is JArray)
                        Merge(existing, item.Value);
                    else
                        current[item.Key] = item.Value;
                }
            }
        }

        public void MergeArrays(JArray current, JArray newer)
        {
            foreach (var sub in newer)
            {
                current.Add(sub);
            }
        }
    }
}
