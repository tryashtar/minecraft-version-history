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

    public MappedClass CopyWith(string oldname, string newname)
    {
        var copy = new MappedClass(oldname, newname);
        foreach (var item in MethodList)
        {
            copy.AddMethod(item.OldName, item.NewName, item.Signature);
        }
        foreach (var item in FieldList)
        {
            copy.AddField(item.OldName, item.NewName);
        }
        return copy;
    }

    public MappedMethod AddMethod(string from, string to, string signature)
    {
        if (from == null || to == null)
            throw new NullReferenceException();
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
        if (from == null || to == null)
            throw new NullReferenceException();
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

    public MappedMethod GetMethod(string from, string signature, Equivalencies eq)
    {
        foreach (var item in eq.GetEquivalentMethods(from))
        {
            if (Methods.TryGetValue((item, signature), out var existing))
                return existing;
        }
        return null;
    }

    public MappedField GetField(string from, Equivalencies eq)
    {
        foreach (var item in eq.GetEquivalentFields(from))
        {
            if (Fields.TryGetValue(item, out var existing))
                return existing;
        }
        return null;
    }
}
