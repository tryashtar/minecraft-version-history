using Microsoft.VisualBasic.FileIO;
using MinecraftVersionHistory;

namespace MCPModernizer;

public abstract class MCP
{
    public static readonly MCPSorter Sorter = new();
    public readonly Sided<FriendlyNames> FriendlyNames = new();
    public readonly Sided<Mappings> LocalMappings = new();
    public string ClientVersion { get; protected set; }

    public void WriteClientMappings(string path)
    {
        using var writer = new StreamWriter(path);
        MappingsIO.WriteTsrg(LocalMappings.Client, writer);
    }
    public void WriteServerMappings(string path)
    {
        using var writer = new StreamWriter(path);
        MappingsIO.WriteTsrg(LocalMappings.Server, writer);
    }
    public void WriteClientFriendlies(string fields, string methods)
    {
        WriteCSVs(fields, methods, FriendlyNames.Client);
    }
    public void WriteServerFriendlies(string fields, string methods)
    {
        WriteCSVs(fields, methods, FriendlyNames.Server);
    }

    private void WriteCSVs(string fields, string methods, FriendlyNames names)
    {
        using var field_writer = new StreamWriter(fields);
        foreach (var item in names.FieldList)
        {
            field_writer.WriteLine(item.Key + "," + item.Value);
        }
        using var method_writer = new StreamWriter(methods);
        foreach (var item in names.MethodList)
        {
            method_writer.WriteLine(item.Key + "," + item.Value);
        }
    }

    protected void ParseCSVs(StreamReader? classes, StreamReader? methods, StreamReader? fields)
    {
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
                        AddToSide(item[4], LocalMappings, x => x.AddClass(item[1], item[3].Replace('/', '.') + '.' + item[0]));
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
                    AddToSide(item[2], FriendlyNames, x => x.AddMethod(item[0], item[1]));
                }
            }
            else if (method_list[0].Length == 9)
            {
                // 3.0 - 5.6 style
                foreach (var item in method_list.Skip(1))
                {
                    AddToSide(item[8], LocalMappings, x => x.GetOrAddClass(item[6].Replace('/', '.')).AddMethod(item[2], item[0], item[4]));
                    AddToSide(item[8], FriendlyNames, x => x.AddMethod(item[0], item[1]));
                }
            }
            else
            {
                // 2.0 - 2.12 style
                // has some weird entries at the end we need to skip
                foreach (var item in method_list.Skip(4).TakeWhile(x => x.Length >= 5))
                {
                    if (item[1] != "*" && item[1] != "")
                        FriendlyNames.Client.AddMethod(item[1], item[4]);
                    if (item[3] != "*" && item[3] != "")
                        FriendlyNames.Server.AddMethod(item[3], item[4]);
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
                    AddToSide(item[2], FriendlyNames, x => x.AddField(name, item[1]));
                }
            }
            else if (field_list[0].Length == 9)
            {
                // 3.0 - 5.6 style
                foreach (var item in field_list.Skip(1))
                {
                    AddToSide(item[8], LocalMappings, x => x.GetOrAddClass(item[6].Replace('/', '.')).AddField(item[2], item[0]));
                    AddToSide(item[8], FriendlyNames, x => x.AddField(item[0], item[1]));
                }
            }
            else
            {
                // 2.0 - 2.12 style
                foreach (var item in field_list.Skip(3))
                {
                    if (item[2] != "*" && item[2] != "")
                        FriendlyNames.Client.AddField(item[2], item[6]);
                    if (item[5] != "*" && item[5] != "")
                        FriendlyNames.Server.AddField(item[5], item[6]);
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
    public int Compare(MCP? x, MCP? y)
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
