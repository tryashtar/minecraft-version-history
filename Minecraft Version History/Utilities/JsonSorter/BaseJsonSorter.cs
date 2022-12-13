namespace MinecraftVersionHistory;

public abstract class BaseJsonSorter : IJsonSorter
{
    public BaseJsonSorter()
    {
    }

    public abstract void Sort(JsonNode root);
}
