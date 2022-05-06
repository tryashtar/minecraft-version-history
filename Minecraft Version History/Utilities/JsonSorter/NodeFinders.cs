namespace MinecraftVersionHistory;

public interface INodeFinder
{
    IEnumerable<(string name, JsonNode node)> FindNodes(JsonNode start);
}

public class ForwardNodeFinder : INodeFinder
{
    private readonly NodeMatcher[] Path;
    public ForwardNodeFinder(IEnumerable<NodeMatcher> path)
    {
        Path = path.ToArray();
    }

    public IEnumerable<(string name, JsonNode node)> FindNodes(JsonNode start)
    {
        IEnumerable<(string name, JsonNode node)> selected = new[] { ((string)null, start) };
        foreach (var matcher in Path)
        {
            selected = matcher.Follow(selected.Select(x => x.node));
        }
        return selected;
    }
}

public class BackwardNodeFinder : INodeFinder
{
    private readonly int Count;
    private readonly ForwardNodeFinder Forward;
    public BackwardNodeFinder(IEnumerable<NodeMatcher> path)
    {
        Count = path.Count();
        Forward = new(path.Reverse());
    }

    public IEnumerable<(string name, JsonNode node)> FindNodes(JsonNode start)
    {
        foreach (var item in AllNodes(null, start))
        {
            if (MatchesPath(item.node))
                yield return item;
        }
    }

    private bool MatchesPath(JsonNode node)
    {
        if (Count == 0)
            return true;
        var parent = node;
        for (int i = 0; i < Count; i++)
        {
            parent = parent.Parent;
            if (parent == null)
                return false;
        }
        return Forward.FindNodes(parent).ToList().Any(x => x.node == node);
    }

    private IEnumerable<(string name, JsonNode node)> AllNodes(string name, JsonNode root)
    {
        yield return (name, root);
        if (root is JsonObject obj)
        {
            foreach (var item in obj)
            {
                foreach (var sub in AllNodes(item.Key, item.Value))
                {
                    yield return sub;
                }
            }
        }
        else if (root is JsonArray arr)
        {
            foreach (var item in arr)
            {
                foreach (var sub in AllNodes(null, item))
                {
                    yield return sub;
                }
            }
        }
    }
}