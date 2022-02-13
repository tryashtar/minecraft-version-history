namespace MinecraftVersionHistory;

public record Rename(string OldName, string NewName);
public class FlatMap
{
    private readonly Dictionary<string, string> Classes = new();
    private readonly Dictionary<string, string> Fields = new();
    private readonly Dictionary<string, string> Methods = new();
    public IEnumerable<Rename> ClassMap => Classes.Select(FromKeyValuePair);
    public IEnumerable<Rename> FieldMap => Fields.Select(FromKeyValuePair);
    public IEnumerable<Rename> MethodMap => Methods.Select(FromKeyValuePair);

    private Rename FromKeyValuePair(KeyValuePair<string, string> pair)
    {
        return new Rename(pair.Key, pair.Value);
    }

    private void Add(Dictionary<string, string> dict, string from, string to)
    {
        if (from != to)
        {
#if DEBUG
            if (dict.TryGetValue(from, out string existing) && existing != to)
                Console.WriteLine($"Remapping {from} from {existing} to {to}");
#endif
            dict[from] = to;
        }
    }

    public void AddClass(string from, string to)
    {
        Add(Classes, from, to);
    }

    public void AddField(string from, string to)
    {
        Add(Fields, from, to);
    }

    public void AddMethod(string from, string to)
    {
        Add(Methods, from, to);
    }

    private string Get(Dictionary<string, string> dict, string name)
    {
        if (dict.TryGetValue(name, out string existing))
            return existing;
        return null;
    }

    public string GetClass(string from)
    {
        return Get(Classes, from);
    }

    public string GetField(string from)
    {
        return Get(Fields, from);
    }

    public string GetMethod(string from)
    {
        return Get(Methods, from);
    }
}
