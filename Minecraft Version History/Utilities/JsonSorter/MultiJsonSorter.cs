namespace MinecraftVersionHistory;

public class MultiJsonSorter : BaseJsonSorter
{
    public readonly IJsonSorter[] Sorters;
    public MultiJsonSorter(DateTime? required, IEnumerable<IJsonSorter> sorters) : base(required)
    {
        Sorters = sorters.ToArray();
    }

    public override void Sort(JObject root)
    {
        foreach (var sorter in Sorters)
        {
            sorter.Sort(root);
        }
    }
}
