namespace MinecraftVersionHistory;

public record Rename(string OldName, string NewName);
public class FlatMap
{
    private readonly Dictionary<string, string> Classes = new();
    private readonly Dictionary<string, string> Fields = new();
    private readonly Dictionary<string, string> Methods = new();
    private readonly List<HashSet<string>> EquivalentClasses = new();
    private readonly List<HashSet<string>> EquivalentFields = new();
    private readonly List<HashSet<string>> EquivalentMethods = new();
    public IEnumerable<Rename> ClassMap => Classes.Select(FromKeyValuePair);
    public IEnumerable<Rename> FieldMap => Fields.Select(FromKeyValuePair);
    public IEnumerable<Rename> MethodMap => Methods.Select(FromKeyValuePair);
    public IEnumerable<IEnumerable<string>> ClassEquivalencies => EquivalentClasses;
    public IEnumerable<IEnumerable<string>> FieldEquivalencies => EquivalentFields;
    public IEnumerable<IEnumerable<string>> MethodEquivalencies => EquivalentMethods;

    private Rename FromKeyValuePair(KeyValuePair<string, string> pair)
    {
        return new Rename(pair.Key, pair.Value);
    }

    private IEnumerable<Rename> GetAll(Dictionary<string, string> dict, List<HashSet<string>> list)
    {
        foreach (var (from, to) in dict)
        {
            var matches = GetEquivalencies(list, from);
            foreach (var item in matches)
            {
                yield return new Rename(item, to);
            }
        }
    }

    public IEnumerable<Rename> GetAllClasses()
    {
        return GetAll(Classes, EquivalentClasses);
    }

    public IEnumerable<Rename> GetAllMethods()
    {
        return GetAll(Methods, EquivalentMethods);
    }

    public IEnumerable<Rename> GetAllFields()
    {
        return GetAll(Fields, EquivalentFields);
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

    private string Get(List<HashSet<string>> list, Dictionary<string, string> dict, string name)
    {
        foreach (var option in GetEquivalencies(list, name))
        {
            if (dict.TryGetValue(option, out string existing))
                return existing;
        }
        return null;
    }

    public string GetClass(string from)
    {
        return Get(EquivalentClasses, Classes, from);
    }

    public string GetField(string from)
    {
        return Get(EquivalentFields, Fields, from);
    }

    public string GetMethod(string from)
    {
        return Get(EquivalentMethods, Methods, from);
    }

    private IEnumerable<string> GetEquivalencies(List<HashSet<string>> list, string name)
    {
        return (IEnumerable<string>)FindSet(list, name) ?? new[] { name };
    }

    public IEnumerable<string> GetEquivalentClasses(string name)
    {
        return GetEquivalencies(EquivalentClasses, name);
    }

    public IEnumerable<string> GetEquivalentMethods(string name)
    {
        return GetEquivalencies(EquivalentMethods, name);
    }

    public IEnumerable<string> GetEquivalentFields(string name)
    {
        return GetEquivalencies(EquivalentFields, name);
    }

    public void AddEquivalentClasses(IEnumerable<string> classes)
    {
        AddEquivalencies(EquivalentClasses, classes);
    }

    public void AddEquivalentMethods(IEnumerable<string> methods)
    {
        AddEquivalencies(EquivalentMethods, methods);
    }

    public void AddEquivalentFields(IEnumerable<string> fields)
    {
        AddEquivalencies(EquivalentFields, fields);
    }

    private HashSet<string> FindOrCreateSet(List<HashSet<string>> list, string entry)
    {
        var set = FindSet(list, entry);
        if (set != null)
            return set;
        var new_set = new HashSet<string>();
        list.Add(new_set);
        return new_set;
    }

    private HashSet<string> FindSet(List<HashSet<string>> list, string entry)
    {
        foreach (var set in list)
        {
            if (set.Contains(entry))
                return set;
        }
        return null;
    }

    private void AddEquivalencies(List<HashSet<string>> list, IEnumerable<string> items)
    {
        if (!items.Any())
            return;
        var set = FindOrCreateSet(EquivalentClasses, items.First());
        foreach (var item in items)
        {
            set.Add(item);
        }
    }
}
