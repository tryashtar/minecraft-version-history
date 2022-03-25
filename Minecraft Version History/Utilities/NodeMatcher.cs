namespace MinecraftVersionHistory;

public abstract class NodeMatcher
{
    public static NodeMatcher Create(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            string val = scalar.Value;
            if (val.StartsWith("@"))
                return new RegexNodeMatcher(new Regex(val[1..]));
            return new NameNodeMatcher(val);
        }
        if (node is YamlMappingNode map)
            return new TemplateNodeMatcher(map);
        throw new ArgumentException(nameof(node));
    }

    public IEnumerable<JToken> Follow(IEnumerable<JToken> starts)
    {
        foreach (var start in starts)
        {
            foreach (var item in Follow(start))
                yield return item;
        }
    }

    public IEnumerable<JProperty> FollowProps(IEnumerable<JProperty> starts)
    {
        foreach (var start in starts)
        {
            foreach (var item in FollowProp(start))
                yield return item;
        }
    }

    public abstract IEnumerable<JToken> Follow(JToken start);
    public abstract IEnumerable<JProperty> FollowProp(JToken start);

    public static IEnumerable<JToken> FollowPath(IEnumerable<NodeMatcher> path, JToken start)
    {
        IEnumerable<JToken> selected = new[] { start };
        foreach (var matcher in path)
        {
            selected = matcher.Follow(selected);
        }
        return selected;
    }

    public static IEnumerable<JProperty> FollowPropPath(IEnumerable<NodeMatcher> path, JToken start)
    {
        IEnumerable<JProperty> selected = path.First().FollowProp(start);
        foreach (var matcher in path.Skip(1))
        {
            selected = matcher.FollowProps(selected);
        }
        return selected;
    }

    public static JObject CreatePath(IEnumerable<NameNodeMatcher> path, JObject start)
    {
        foreach (var item in path)
        {
            JObject next;
            if (start.TryGetValue(item.Name, out var existing))
                next = (JObject)existing;
            else
            {
                next = new JObject();
                start.Add(item.Name, next);
            }
            start = next;
        }
        return start;
    }
}

public class NameNodeMatcher : NodeMatcher
{
    public readonly string Name;
    public NameNodeMatcher(string name)
    {
        Name = name;
    }

    public override IEnumerable<JToken> Follow(JToken start) => FollowProp(start).Select(x => x.Value);

    public override IEnumerable<JProperty> FollowProp(JToken start)
    {
        if (start is JObject obj)
        {
            var prop = obj.Property(Name);
            if (prop != null)
                yield return prop;
        }
    }
}

public class RegexNodeMatcher : NodeMatcher
{
    public readonly Regex Regex;
    public RegexNodeMatcher(Regex regex)
    {
        Regex = regex;
    }

    public override IEnumerable<JToken> Follow(JToken start) => FollowProp(start).Select(x => x.Value);

    public override IEnumerable<JProperty> FollowProp(JToken start)
    {
        if (start is JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                if (Regex.IsMatch(prop.Name))
                    yield return prop;
            }
        }
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

    public override IEnumerable<JProperty> FollowProp(JToken start)
    {
        yield break;
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
