namespace MinecraftVersionHistory;

public abstract class BaseJsonSorter : IJsonSorter
{
    public readonly DateTime? RequiredTime;

    public BaseJsonSorter(DateTime? required)
    {
        RequiredTime = required;
    }

    public bool ShouldSort(Version version)
    {
        return RequiredTime == null || version.ReleaseTime >= RequiredTime;
    }

    public abstract void Sort(JsonObject root);
}
