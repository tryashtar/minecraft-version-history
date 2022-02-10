
namespace MinecraftVersionHistory;

public static class MappingsIO
{
    public static void ParseTsrg(Mappings mappings, StreamReader reader)
    {
        MappedClass current_class = null;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            if (line.StartsWith('\t'))
            {
                var entries = line.TrimStart('\t').Split(' ');
                if (entries.Length == 2)
                    current_class.AddField(entries[0], entries[1]);
                else if (entries.Length == 3)
                    current_class.AddMethod(entries[0], entries[2], entries[1]);
            }
            else
            {
                var entries = line.Split(' ');
                current_class = mappings.AddClass(entries[0].Replace('/', '.'), entries[1].Replace('/', '.'));
            }
        }
    }

    public static void ParseSrg(Mappings mappings, StreamReader reader)
    {
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            var entries = line.Split(' ');
            var type = entries[0];
            if (type == "CL:")
                mappings.AddClass(entries[1].Replace('/', '.'), entries[2].Replace('/', '.'));
            else if (type == "FD:")
            {
                var (cpath, name) = Split(entries[1]);
                mappings.GetOrAddClass(cpath).AddField(name, Split(entries[2]).name);
            }
            else if (type == "MD:")
            {
                var (cpath, name) = Split(entries[1]);
                mappings.GetOrAddClass(cpath).AddMethod(name, Split(entries[3]).name, entries[2]);
            }
        }
    }

    public static void WriteTsrg(Mappings mappings, StreamWriter writer)
    {
        foreach (var c in mappings.ClassList)
        {
            writer.WriteLine($"{c.OldName.Replace('.', '/')} {c.NewName.Replace('.', '/')}");
            foreach (var f in c.FieldList)
            {
                writer.WriteLine($"\t{f.OldName} {f.NewName}");
            }
            foreach (var m in c.MethodList)
            {
                writer.WriteLine($"\t{m.OldName} {m.Signature} {m.NewName}");
            }
        }
    }

    public static (string classpath, string name) Split(string path)
    {
        int sep = path.LastIndexOf('/');
        if (sep == -1)
            return (null, path);
        return (path[..sep].Replace('/', '.'), path[(sep + 1)..]);
    }

    public static void ParseProguard(Mappings mappings, StreamReader reader)
    {
        MappedClass current_class = null;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            if (line.StartsWith("    "))
            {
                var entries = line.TrimStart(' ').Split(" -> ");
                if (entries[0].Contains('('))
                {
                    var split = entries[0].Split(' ');
                    int paren = split[1].IndexOf('(');
                    string name = split[1][..paren];
                    string sig = split[0] + split[1][paren..];
                    current_class.AddMethod(entries[1], name, sig);
                }
                else
                    current_class.AddField(entries[1], entries[0].Split(' ')[1]);
            }
            else
            {
                var entries = line.Split(" -> ");
                current_class = mappings.AddClass(entries[1].TrimEnd(':'), entries[0]);
            }
        }
    }

    public static void WriteCSVs(FlatMap names, StreamWriter fields, StreamWriter methods)
    {
        foreach (var item in names.FieldList)
        {
            fields.WriteLine(item.OldName + "," + item.NewName);
        }
        foreach (var item in names.MethodList)
        {
            methods.WriteLine(item.OldName + "," + item.NewName);
        }
    }

    public static void ParseCSVs(FlatMap names, StreamReader fields, StreamReader methods)
    {
        while (!fields.EndOfStream)
        {
            var items = fields.ReadLine().Split(',');
            names.AddField(items[0], items[1]);
        }
        while (!methods.EndOfStream)
        {
            var items = methods.ReadLine().Split(',');
            names.AddMethod(items[0], items[1]);
        }
    }
}
