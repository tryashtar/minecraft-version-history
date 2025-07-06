namespace MinecraftVersionHistory;

public class Mappings
{
    private readonly Dictionary<string, MappedClass> Classes = new();
    public IEnumerable<MappedClass> ClassList => Classes.Values;

    public MappedClass GetClass(string from, Equivalencies eq)
    {
        foreach (var item in eq.GetEquivalentClasses(from))
        {
            if (Classes.TryGetValue(item, out var existing))
                return existing;
        }
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
        if (from == null || to == null)
            throw new NullReferenceException();
        MappedClass newclass;
        if (Classes.TryGetValue(from, out var existing))
        {
#if DEBUG
            if (to != existing.NewName)
                Console.WriteLine($"Changing {from} from {existing.NewName} to {to}");
#endif
            newclass = existing.CopyWith(from, to);
        }
        else
            newclass = new MappedClass(from, to);
        Classes[from] = newclass;
        return newclass;
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
}
