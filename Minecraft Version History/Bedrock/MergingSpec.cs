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

        public void Merge(JObject current, JObject newer)
        {
            foreach (var item in newer)
            {
                bool exists = current.TryGetValue(item.Key, out var existing);
                if (!exists || OverwriteKeys.Contains(item.Key))
                    current[item.Key] = item.Value;
                else
                {
                    if (item.Value is JArray array)
                    {
                        var existing_array = (JArray)existing;
                        foreach (var sub in item.Value) existing_array.Add(sub);
                    }
                    else if (item.Value is JObject obj)
                        Merge((JObject)existing, obj);
                    else
                        current[item.Key] = item.Value;
                }
            }
        }
    }
}
