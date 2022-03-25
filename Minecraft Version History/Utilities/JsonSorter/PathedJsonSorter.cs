namespace MinecraftVersionHistory;

public abstract class PathedJsonSorter : BaseJsonSorter
{
    public readonly NodeMatcher[] Path;
    public PathedJsonSorter(DateTime? required, IEnumerable<NodeMatcher> path) : base(required)
    {
        Path = path.ToArray();
    }

    public override void Sort(JObject root)
    {
        var selected = NodeMatcher.FollowPath(Path, root);
        foreach (var item in selected)
        {
            SortSelected(item);
        }
    }

    public abstract void SortSelected(JToken token);
}
