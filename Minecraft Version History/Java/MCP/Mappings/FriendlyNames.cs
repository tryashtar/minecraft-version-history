namespace MinecraftVersionHistory;

public class FriendlyNames
{
    public IEnumerable<KeyValuePair<string, string>> FieldList => Fields;
    public IEnumerable<KeyValuePair<string, string>> MethodList => Methods;
    private readonly Dictionary<string, string> Fields = new();
    private readonly Dictionary<string, string> Methods = new();
    public void AddField(string from, string to)
    {
        if (from != to)
        {
#if DEBUG
            if (Fields.TryGetValue(to, out string existing))
                Console.WriteLine($"Replacing friendly name for {from} from {existing} to {to}");
#endif
            Fields[from] = to;
        }
    }
    public void AddMethod(string from, string to)
    {
        if (from != to)
        {
#if DEBUG
            if (Methods.TryGetValue(to, out string existing))
                Console.WriteLine($"Replacing friendly name for {from} from {existing} to {to}");
#endif
            Methods[from] = to;
        }
    }
}
