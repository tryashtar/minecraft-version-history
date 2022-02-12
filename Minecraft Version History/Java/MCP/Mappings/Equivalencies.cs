namespace MinecraftVersionHistory;

public class Equivalencies
{
    private readonly List<HashSet<string>> EquivalentClasses;
    private readonly List<HashSet<string>> EquivalentFields;
    private readonly List<HashSet<string>> EquivalentMethods;
    public IEnumerable<IEnumerable<string>> Classes => EquivalentClasses;
    public IEnumerable<IEnumerable<string>> Fields => EquivalentFields;
    public IEnumerable<IEnumerable<string>> Methods => EquivalentMethods;

    public Equivalencies()
    {
        EquivalentClasses = new();
        EquivalentFields = new();
        EquivalentMethods = new();
    }

    public Equivalencies(IEnumerable<IEnumerable<string>> classes, IEnumerable<IEnumerable<string>> fields, IEnumerable<IEnumerable<string>> methods)
    {
        EquivalentClasses = classes.Select(x => new HashSet<string>(x)).ToList();
        EquivalentFields = fields.Select(x => new HashSet<string>(x)).ToList();
        EquivalentMethods = methods.Select(x => new HashSet<string>(x)).ToList();
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

    public void AddClasses(IEnumerable<string> classes)
    {
        AddEquivalencies(EquivalentClasses, classes);
    }

    public void AddMethods(IEnumerable<string> methods)
    {
        AddEquivalencies(EquivalentMethods, methods);
    }

    public void AddFields(IEnumerable<string> fields)
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
        var set = FindOrCreateSet(list, items.First());
        foreach (var item in items)
        {
            set.Add(item);
        }
    }
}
