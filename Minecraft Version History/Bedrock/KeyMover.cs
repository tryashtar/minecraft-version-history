namespace MinecraftVersionHistory;

public class KeyMover
{
    private readonly NodeMatcher[] Include;
    private readonly NameNodeMatcher[] Destination;
    public KeyMover(YamlMappingNode node)
    {
        Include = node.Go("from").ToList(x => NodeMatcher.Create(x)).ToArray();
        Destination = node.Go("to").ToList(x => new NameNodeMatcher(x.String())).ToArray();
    }

    public void MoveKeys(JObject obj)
    {
        var moving = NodeMatcher.FollowPropPath(Include, obj).ToList();
        var destination = NodeMatcher.CreatePath(Destination, obj);
        foreach (var item in moving)
        {
            item.Remove();
            destination.Add(item.Name, item.Value);
        }
    }
}
