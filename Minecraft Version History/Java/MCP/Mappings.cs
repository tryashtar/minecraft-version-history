namespace MinecraftVersionHistory;

public class SidedMappings
{
    public readonly Mappings Client = new();
    public readonly Mappings Server = new();
}

public class Mappings
{
    private readonly Dictionary<string, MappedClass> Classes = new();
    private readonly Dictionary<string, MappedClass> MethodMap = new();
    private readonly Dictionary<string, MappedClass> FieldMap = new();
    public IEnumerable<MappedClass> ClassList => Classes.Values;

    public void AddClass(string from, string to)
    {
        if (Classes.TryGetValue(from, out var existing))
        {
#if DEBUG
            if (to != existing.NewName)
                Console.WriteLine($"Ignoring change of {from} from {existing.NewName} to {to}");
#endif
        }
        else
            Classes.Add(from, new MappedClass(from, to));
    }

    public void AddMethod(string from, string to, string signature)
    {
        var (path, name) = Split(from);
        (_, to) = Split(to);
        if (name != to)
        {
            if (path != null && !Classes.ContainsKey(path))
                Classes.Add(path, new MappedClass(path, path));
            Classes[path].AddMethod(name, to, signature);
            MethodMap[to] = Classes[path];
        }
    }

    public void AddField(string from, string to)
    {
        var (path, name) = Split(from);
        (_, to) = Split(to);
        if (name != to)
        {
            if (path != null && !Classes.ContainsKey(path))
                Classes.Add(path, new MappedClass(path, path));
            Classes[path].AddField(name, to);
            FieldMap[to] = Classes[path];
        }
    }

    public void RemapField(string from, string to)
    {
        if (from != to)
        {
            var owner = FieldMap[from];
            var old_field = owner.GetField(from);
            owner.AddField(old_field.OldName, to);
        }
    }

    public void RemapMethod(string from, string to)
    {
        if (from != to)
        {
            var owner = MethodMap[from];
            var overloads = owner.GetOverloads(from).ToList();
            foreach (var o in overloads)
            {
                owner.AddMethod(o.OldName, to, o.Signature);
            }
        }
    }

    public static (string classpath, string name) Split(string path)
    {
        int sep = path.LastIndexOf('/');
        if (sep == -1)
            return (null, path);
        return (path[..sep], path[(sep + 1)..]);
    }
}

public record MappedField(string OldName, string NewName);
public record MappedMethod(string OldName, string NewName, string Signature);

public record MappedClass(string OldName, string NewName)
{
    private readonly Dictionary<(string name, string signature), MappedMethod> Methods = new();
    private readonly Dictionary<string, List<MappedMethod>> NewOverloads = new();
    private readonly Dictionary<string, MappedField> Fields = new();
    private readonly Dictionary<string, MappedField> NewFields = new();
    public IEnumerable<MappedMethod> MethodList => Methods.Values;
    public IEnumerable<MappedField> FieldList => Fields.Values;

    public IEnumerable<MappedMethod> GetOverloads(string name) => NewOverloads[name];
    public MappedField GetField(string name) => NewFields[name];

    public void AddMethod(string from, string to, string signature)
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
        if (!NewOverloads.ContainsKey(to))
            NewOverloads[to] = new();
        NewOverloads[to].Add(method);
    }

    public void AddField(string from, string to)
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
        NewFields[to] = field;
    }
}
