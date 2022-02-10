namespace MinecraftVersionHistory;

public class Mappings
{
    private readonly Dictionary<string, MappedClass> Classes = new();
    public IEnumerable<MappedClass> ClassList => Classes.Values;

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
}
