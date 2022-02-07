namespace MinecraftVersionHistory;

public class SidedMappings<T> where T : new()
{
    public readonly T Client = new();
    public readonly T Server = new();
    public T Get(string side)
    {
        return side switch
        {
            "0" => Client,
            "1" => Server,
            _ => throw new ArgumentException()
        };
    }
}

public class UntargetedMappings
{
    public readonly Dictionary<string, string> Methods = new();
    public readonly Dictionary<string, string> Fields = new();

    public void AddMethod(string from, string to)
    {
        Methods.Add(from, to);
    }

    public void AddField(string from, string to)
    {
        Fields.Add(from, to);
    }
}

public class TargetedMappings
{
    public readonly Dictionary<string, MappedClass> Classes = new();

    public void AddClass(string from, string to)
    {
        Classes.Add(from, new MappedClass(from, to));
    }

    public void AddMethod(string from, string to, string signature)
    {
        var (path, name) = Split(from);
        Classes[path].AddMethod(name, to, signature);
    }

    public void AddField(string from, string to)
    {
        var (path, name) = Split(from);
        Classes[path].AddField(name, to);
    }

    public TargetedMappings Remap(UntargetedMappings untargeted)
    {
        var result = new TargetedMappings();
        foreach (var c in Classes.Values)
        {
            result.AddClass(c.OldName, c.NewName);
            foreach (var f in c.Fields)
            {
                var value = untargeted.Fields.GetValueOrDefault(f.Key, f.Value);
                result.AddField(f.Key, value);
            }
            foreach (var m in c.Methods.Values)
            {
                var value = untargeted.Methods.GetValueOrDefault(m.OldName, m.NewName);
                result.AddField(m.OldName, value);
            }
        }
        return result;
    }

    private (string classpath, string name) Split(string path)
    {
        int sep = path.LastIndexOf('/');
        return (path[..sep], path[(sep + 1)..]);
    }
}

public record MappedMethod(string OldName, string NewName, string Signature);

public record MappedClass(string OldName, string NewName)
{
    public readonly Dictionary<string, MappedMethod> Methods = new();
    public readonly Dictionary<string, string> Fields = new();

    public void AddMethod(string from, string to, string signature)
    {
        Methods.Add(from, new MappedMethod(from, to, signature));
    }

    public void AddField(string from, string to)
    {
        Fields.Add(from, to);
    }
}
