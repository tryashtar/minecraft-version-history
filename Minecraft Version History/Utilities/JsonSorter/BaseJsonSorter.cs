namespace MinecraftVersionHistory;

public abstract class BaseJsonSorter : IJsonSorter
{
    public readonly SorterRequirements Requirements;

    public BaseJsonSorter(SorterRequirements required)
    {
        Requirements = required;
    }

    public bool ShouldSort(Version version)
    {
        return Requirements.MetBy(version);
    }

    public bool ShouldSort(string path)
    {
        return Requirements.MetBy(path);
    }

    public abstract void Sort(JsonObject root);
}
