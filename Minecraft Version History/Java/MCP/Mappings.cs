namespace MinecraftVersionHistory;

public class SidedMappings<T> where T : new()
{
    public readonly T Client = new();
    public readonly T Server = new();
}

public class UntargetedMappings
{
    public readonly Dictionary<string, string> Methods = new();
    public readonly Dictionary<string, string> Fields = new();

    public void AddMethod(string from, string to)
    {
        if (from != to)
        {
            if (Methods.TryGetValue(from, out var existing))
            {
                if (to != existing)
                    Console.WriteLine($"Replacing {from} from {existing} to {to}");
            }
            Methods[from] = to;
        }
    }

    public void AddField(string from, string to)
    {
        if (from != to)
        {
            if (Methods.TryGetValue(from, out var existing))
            {
                if (to != existing)
                    Console.WriteLine($"Replacing {from} from {existing} to {to}");
            }
            Fields[from] = to;
        }
    }
}

public class TargetedMappings
{
    public readonly Dictionary<string, MappedClass> Classes = new();

    public void AddClass(string from, string to)
    {
        if (Classes.TryGetValue(from, out var existing))
        {
            if (to != existing.NewName)
                Console.WriteLine($"Ignoring change of {from} from {existing.NewName} to {to}?");
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
        }
    }

    public TargetedMappings Remap(UntargetedMappings untargeted)
    {
        var result = new TargetedMappings();
        foreach (var c in Classes.Values)
        {
            result.AddClass(c.OldName, c.NewName);
            foreach (var f in c.Fields)
            {
                var value = untargeted.Fields.GetValueOrDefault(f.Value, f.Value);
                if (value != f.Value)
                    Console.WriteLine($"Remapped {f.Value} to {value}");
                result.AddField(c.OldName + "/" + f.Key, value);
            }
            foreach (var m in c.Methods.Values)
            {
                var value = untargeted.Methods.GetValueOrDefault(m.NewName, m.NewName);
                if (value != m.NewName)
                    Console.WriteLine($"Remapped {m.NewName} to {value}");
                result.AddMethod(c.OldName + "/" + m.OldName, value, m.Signature);
            }
        }
        return result;
    }

    public static (string classpath, string name) Split(string path)
    {
        int sep = path.LastIndexOf('/');
        if (sep == -1)
            return (null, path);
        return (path[..sep], path[(sep + 1)..]);
    }
}

public record MappedMethod(string OldName, string NewName, string Signature);

public record MappedClass(string OldName, string NewName)
{
    public readonly Dictionary<(string name, string signature), MappedMethod> Methods = new();
    public readonly Dictionary<string, string> Fields = new();

    public void AddMethod(string from, string to, string signature)
    {
        if (Methods.TryGetValue((from, signature), out var existing))
        {
            if (to != existing.NewName)
                Console.WriteLine($"Changing {from} from {existing} to {to}");
        }
        Methods[(from, signature)] = new MappedMethod(from, to, signature);
    }

    public void AddField(string from, string to)
    {
        if (Fields.TryGetValue(from, out var existing))
        {
            if (to != existing)
                Console.WriteLine($"Changing {from} from {existing} to {to}");
        }
        Fields[from] = to;
    }
}
