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
            if (val == "*")
                return new AlwaysNodeMatcher();
            return new NameNodeMatcher(val);
        }
        if (node is YamlMappingNode map)
            return new TemplateNodeMatcher(map);
        throw new ArgumentException(nameof(node));
    }

    public abstract bool Matches(string name, JsonNode node);

    public IEnumerable<(string name, JsonNode node)> Follow(JsonNode start)
    {
        if (start is JsonObject obj)
            return obj.Where(x => Matches(x.Key, x.Value)).Select(x => (x.Key, x.Value));
        else if (start is JsonArray arr)
            return arr.Where(x => Matches(null, x)).Select(x => ((string)null, x));
        return Array.Empty<(string, JsonNode)>();
    }

    public IEnumerable<(string name, JsonNode node)> Follow(IEnumerable<JsonNode> starts)
    {
        foreach (var start in starts)
        {
            foreach (var item in Follow(start))
                yield return item;
        }
    }
}

public class NameNodeMatcher : NodeMatcher
{
    public readonly string Name;
    public NameNodeMatcher(string name)
    {
        Name = name;
    }

    public override bool Matches(string name, JsonNode node)
    {
        if (name == Name)
            return true;
        if (node.Parent is JsonArray arr && int.TryParse(Name, out int index) && arr[index] == node)
            return true;
        return false;
    }

    public static JsonObject CreatePath(IEnumerable<NameNodeMatcher> path, JsonObject start)
    {
        foreach (var item in path)
        {
            JsonObject next;
            if (start.TryGetPropertyValue(item.Name, out var existing))
                next = (JsonObject)existing;
            else
            {
                next = new JsonObject();
                start.Add(item.Name, next);
            }
            start = next;
        }
        return start;
    }
}

public class RegexNodeMatcher : NodeMatcher
{
    public readonly Regex RegExpression;
    public RegexNodeMatcher(Regex regex)
    {
        RegExpression = regex;
    }

    public override bool Matches(string name, JsonNode node)
    {
        if (name == null)
            return false;
        return RegExpression.IsMatch(name);
    }
}

public class TemplateNodeMatcher : NodeMatcher
{
    private readonly YamlMappingNode Template;
    public TemplateNodeMatcher(YamlMappingNode template)
    {
        Template = template;
    }

    private static bool ValueEquals(JsonNode json, YamlNode yaml)
    {
        if (yaml is YamlScalarNode scalar)
            return (string)scalar == json.ToString();
        throw new ArgumentException();
    }

    public override bool Matches(string name, JsonNode node)
    {
        if (node is not JsonObject obj)
            return false;
        foreach (var item in Template)
        {
            if (!(obj.TryGetPropertyValue((string)item.Key, out var value) && ValueEquals(value, item.Value)))
                return false;
        }
        return true;
    }
}

public class AlwaysNodeMatcher : NodeMatcher
{
    public override bool Matches(string name, JsonNode node)
    {
        return true;
    }
}
