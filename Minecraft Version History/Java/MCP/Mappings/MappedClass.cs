namespace MinecraftVersionHistory;

public record MappedField(string OldName, string NewName);
public record MappedMethod(string OldName, string NewName, string Signature);
public class MappedClass
{
    public readonly string OldName;
    public readonly string NewName;
    private readonly Dictionary<(string name, string signature), MappedMethod> Methods = new();
    private readonly Dictionary<string, MappedField> Fields = new();
    public MappedClass(string oldname, string newname)
    {
        OldName = oldname;
        NewName = newname;
    }

    public IEnumerable<MappedMethod> MethodList => Methods.Values;
    public IEnumerable<MappedField> FieldList => Fields.Values;

    public MappedMethod AddMethod(string from, string to, string signature)
    {
#if DEBUG
        if (Methods.TryGetValue((from, signature), out var existing))
        {
            if (to != existing.NewName)
                Console.WriteLine($"Changing {from} from {existing.NewName} to {to}");
        }
#endif
        var method = new MappedMethod(from, to, signature);
        Methods[(from, signature)] = method;
        return method;
    }

    public MappedField AddField(string from, string to)
    {
#if DEBUG
        if (Fields.TryGetValue(from, out var existing))
        {
            if (to != existing.NewName)
                Console.WriteLine($"Changing {from} from {existing.NewName} to {to}");
        }
#endif
        var field = new MappedField(from, to);
        Fields[from] = field;
        return field;
    }
}
