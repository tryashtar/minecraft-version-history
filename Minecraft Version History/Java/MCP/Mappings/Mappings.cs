namespace MinecraftVersionHistory;

public class Mappings
{
    private readonly Dictionary<string, MappedClass> Classes = new();
    public IEnumerable<MappedClass> ClassList => Classes.Values;

    public MappedClass GetClass(string from)
    {
        if (Classes.TryGetValue(from, out var existing))
            return existing;
        return null;
    }

    public MappedClass GetOrAddClass(string from)
    {
        if (Classes.TryGetValue(from, out var existing))
            return existing;
        return AddClass(from, from);
    }

    public MappedClass AddClass(string from, string to)
    {
        if (Classes.TryGetValue(from, out var existing))
        {
#if DEBUG
            if (to != existing.NewName)
                Console.WriteLine($"Ignoring change of {from} from {existing.NewName} to {to}");
#endif
            return existing;
        }
        else
        {
            var newclass = new MappedClass(from, to);
            Classes.Add(from, newclass);
            return newclass;
        }
    }

    public Mappings Reversed()
    {
        var mappings = new Mappings();
        foreach (var c in Classes.Values)
        {
            var n = mappings.AddClass(c.NewName, c.OldName);
            foreach (var f in c.FieldList)
            {
                n.AddField(f.NewName, f.OldName);
            }
            foreach (var m in c.MethodList)
            {
                n.AddMethod(m.NewName, m.OldName, m.Signature);
            }
        }
        return mappings;
    }

    public FlatMap Flattened()
    {
        var map = new FlatMap();
        foreach (var c in Classes.Values)
        {
            map.AddClass(c.OldName, c.NewName);
            foreach (var f in c.FieldList)
            {
                map.AddField(f.OldName, f.NewName);
            }
            foreach (var m in c.MethodList)
            {
                map.AddMethod(m.OldName, m.NewName);
            }
        }
        return map;
    }
}
