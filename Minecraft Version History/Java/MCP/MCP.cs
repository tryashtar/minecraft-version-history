namespace MinecraftVersionHistory;

public abstract class MCP
{
    public static readonly MCPSorter Sorter = new();
    public static readonly Sided<FriendlyNames> GlobalFriendlies = new();
    public readonly Lazy<Sided<Mappings>> LocalMappings;
    protected abstract Sided<Mappings> LoadMappings();
    public void EnsureLoaded()
    {
        LocalMappings.Value.ToString();
    }

    public MCP()
    {
        LocalMappings = new(LoadMappings);
    }

    public abstract bool AcceptsVersion(JavaVersion version);
    public void CreateClientMappings(string path)
    {
        GlobalFriendlies.Client.ApplyTo(LocalMappings.Value.Client);
        WriteSRG(path, LocalMappings.Value.Client);
    }
    public void CreateServerMappings(string path)
    {
        GlobalFriendlies.Server.ApplyTo(LocalMappings.Value.Server);
        WriteSRG(path, LocalMappings.Value.Server);
    }

    protected void WriteSRG(string path, Mappings mappings)
    {
        using var writer = new StreamWriter(path);
        // export as TSRG format, which resembles proguard more than SRG
        // each class lists its properties in turn instead of duplicating the class name for each
        // also we don't need the deobfuscated method signature
        foreach (var c in mappings.ClassList)
        {
            writer.WriteLine($"{Mappings.Split(c.OldName).name} {c.NewName}");
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

    protected void ParseCSVs(Sided<Mappings> mappings, StreamReader newids, StreamReader classes, StreamReader methods, StreamReader fields)
    {
        if (newids != null)
        {
            var ids = ParseCSV(newids).ToList();
            foreach (var item in ids.Skip(1))
            {
                if (item[0] != "*")
                    GlobalFriendlies.Client.AddRename(item[0], item[2]);
                if (item[1] != "*")
                    GlobalFriendlies.Server.AddRename(item[1], item[2]);
            }
        }
        if (classes != null)
        {
            var class_list = ParseCSV(classes).ToList();
            if (class_list[0][0] == "name")
            {
                // 3.0 - 5.6 style
                foreach (var item in class_list.Skip(1))
                {
                    // skip lines with no destination package (a few random ones that clearly aren't classes)
                    if (item[3] != "")
                        AddToSide(item[4], mappings, x => x.AddClass(item[3] + "/" + item[1], item[3] + "/" + item[0]));
                }
            }
        }
        if (methods != null)
        {
            var method_list = ParseCSV(methods).ToList();
            if (method_list[0].Length == 4)
            {
                // 6.0+ style
                foreach (var item in method_list.Skip(1))
                {
                    AddToSide(item[2], GlobalFriendlies, x => x.AddMethod(item[0], item[1]));
                }
            }
            else if (method_list[0].Length == 9)
            {
                // 3.0 - 5.6 style
                foreach (var item in method_list.Skip(1))
                {
                    AddToSide(item[8], mappings, x => x.AddMethod(item[7] + "/" + item[6] + "/" + item[2], item[0], item[4]));
                    AddToSide(item[8], GlobalFriendlies, x => x.AddMethod(item[0], item[1]));
                }
            }
            else
            {
                // 2.0 - 2.12 style
                // has some weird entries at the end we need to skip
                foreach (var item in method_list.Skip(4).Where(x => x.Length >= 5))
                {
                    if (item[1] != "*" && item[1] != "")
                        GlobalFriendlies.Client.AddMethod(item[1], item[4]);
                    if (item[3] != "*" && item[3] != "")
                        GlobalFriendlies.Server.AddMethod(item[3], item[4]);
                }
            }
        }
        if (fields != null)
        {
            var field_list = ParseCSV(fields).ToList();
            if (field_list[0].Length == 4)
            {
                // 6.0+ style
                foreach (var item in field_list.Skip(1))
                {
                    // fix some modern ones prefixing fields
                    var name = item[0];
                    int dot = name.IndexOf('.');
                    if (dot != -1)
                        name = name[(dot + 1)..];
                    AddToSide(item[2], GlobalFriendlies, x => x.AddField(name, item[1]));
                }
            }
            else if (field_list[0].Length == 9)
            {
                // 3.0 - 5.6 style
                foreach (var item in field_list.Skip(1))
                {
                    AddToSide(item[8], mappings, x => x.AddField(item[7] + "/" + item[6] + "/" + item[2], item[0]));
                    AddToSide(item[8], GlobalFriendlies, x => x.AddField(item[0], item[1]));
                }
            }
            else
            {
                // 2.0 - 2.12 style
                foreach (var item in field_list.Skip(3))
                {
                    if (item[2] != "*" && item[2] != "")
                        GlobalFriendlies.Client.AddField(item[2], item[6]);
                    if (item[5] != "*" && item[5] != "")
                        GlobalFriendlies.Server.AddField(item[5], item[6]);
                }
            }
        }
    }

    private void AddToSide<T>(string side, Sided<T> sided, Action<T> action) where T : new()
    {
        if (side == "0" || side == "2")
            action(sided.Client);
        if (side == "1" || side == "2")
            action(sided.Server);
    }

    private IEnumerable<string[]> ParseCSV(StreamReader reader)
    {
        var parser = new TextFieldParser(reader);
        parser.HasFieldsEnclosedInQuotes = true;
        parser.SetDelimiters(",");
        while (!parser.EndOfData)
        {
            yield return parser.ReadFields();
        }
    }
}

public class MCPSorter : IComparer<MCP>
{
    public int Compare(MCP x, MCP y)
    {
        if (x is ClassicMCP cx && y is ClassicMCP cy)
        {
            int m = cx.MajorVersion.CompareTo(cy.MajorVersion);
            if (m != 0)
                return m;
            int m2 = cx.MinorVersion.CompareTo(cy.MinorVersion);
            if (m2 != 0)
                return m2;
            return cx.ExtraVersion.CompareTo(cy.ExtraVersion);
        }
        return 0;
    }
}
