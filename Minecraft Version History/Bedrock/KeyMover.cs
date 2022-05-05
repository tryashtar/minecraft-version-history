namespace MinecraftVersionHistory;

public class KeyMover
{
    private readonly INodeFinder Include;
    private readonly NameNodeMatcher[] Destination;
    public KeyMover(YamlMappingNode node)
    {
        Include = new ForwardNodeFinder(node.Go("from").ToList(x => NodeMatcher.Create(x)));
        Destination = node.Go("to").ToList(x => new NameNodeMatcher(x.String())).ToArray();
    }

    public void MoveKeys(JsonObject obj)
    {
        var moving = Include.FindNodes(obj).ToList();
        var destination = NameNodeMatcher.CreatePath(Destination, obj);
        foreach (var (name, node) in moving)
        {
            node.Parent.AsObject().Remove(name);
            destination.Add(name, node);
        }
    }
}
