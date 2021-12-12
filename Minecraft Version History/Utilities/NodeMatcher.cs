using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public abstract class NodeMatcher
    {
        public static NodeMatcher Create(YamlNode node)
        {
            if (node is YamlScalarNode scalar)
                return new NameNodeMatcher((string)scalar);
            if (node is YamlMappingNode map)
                return new TemplateNodeMatcher(map);
            throw new ArgumentException(nameof(node));
        }

        public IEnumerable<JToken> Follow(IEnumerable<JToken> starts)
        {
            foreach (var start in starts)
            {
                foreach (var item in Follow(start)) yield return item;
            }
        }

        public abstract IEnumerable<JToken> Follow(JToken start);
    }

    public class NameNodeMatcher : NodeMatcher
    {
        public readonly string Name;
        public NameNodeMatcher(string name)
        {
            Name = name;
        }

        public override IEnumerable<JToken> Follow(JToken start)
        {
            if (start is JObject obj && obj.TryGetValue(Name, out var result))
                yield return result;
        }
    }

    public class TemplateNodeMatcher : NodeMatcher
    {
        private readonly YamlMappingNode Template;
        public TemplateNodeMatcher(YamlMappingNode template)
        {
            Template = template;
        }

        public override IEnumerable<JToken> Follow(JToken start)
        {
            if (start is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is JObject obj && Matches(obj))
                        yield return obj;
                }
            }
        }

        private bool Matches(JObject obj)
        {
            foreach (var item in Template)
            {
                if (!obj.TryGetValue((string)item.Key, out var value) && ValueEquals(value, item.Value))
                    return false;
            }
            return true;
        }

        private static bool ValueEquals(JToken json, YamlNode yaml)
        {
            if (yaml is YamlScalarNode scalar)
                return (string)scalar == json.ToString();
            throw new ArgumentException();
        }
    }
}
