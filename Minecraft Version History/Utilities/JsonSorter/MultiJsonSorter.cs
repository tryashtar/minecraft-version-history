namespace MinecraftVersionHistory;

public class MultiJsonSorter : BaseJsonSorter
{
    public readonly IJsonSorter[] Sorters;
    public MultiJsonSorter(IEnumerable<IJsonSorter> sorters) : base()
    {
        Sorters = sorters.ToArray();
    }

    public override void Sort(JsonNode root)
    {
        foreach (var sorter in Sorters)
        {
            sorter.Sort(root);
        }
    }
}
