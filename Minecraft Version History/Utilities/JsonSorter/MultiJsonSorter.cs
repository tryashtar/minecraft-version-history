namespace MinecraftVersionHistory;

public class MultiJsonSorter : BaseJsonSorter
{
    public readonly IJsonSorter[] Sorters;
    public MultiJsonSorter(SorterRequirements required, IEnumerable<IJsonSorter> sorters) : base(required)
    {
        Sorters = sorters.ToArray();
    }

    public override void Sort(JsonObject root)
    {
        foreach (var sorter in Sorters)
        {
            sorter.Sort(root);
        }
    }
}
