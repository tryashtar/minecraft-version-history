namespace MinecraftVersionHistory;

public record Rename(string OldName, string NewName);
public class FlatMap
{
    public IEnumerable<Rename> ClassList => Classes.Select(FromKeyValuePair);
    public IEnumerable<Rename> FieldList => Fields.Select(FromKeyValuePair);
    public IEnumerable<Rename> MethodList => Methods.Select(FromKeyValuePair);
    private readonly Dictionary<string, string> Classes = new();
    private readonly Dictionary<string, string> Fields = new();
    private readonly Dictionary<string, string> Methods = new();

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

    public string GetClass(string from)
    {
        if (Classes.TryGetValue(from, out string existing))
            return existing;
        return null;
    }

    public string GetField(string from)
    {
        if (Fields.TryGetValue(from, out string existing))
            return existing;
        return null;
    }

    public string GetMethod(string from)
    {
        if (Methods.TryGetValue(from, out string existing))
            return existing;
        return null;
    }
}
