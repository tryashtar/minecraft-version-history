namespace MinecraftVersionHistory;

public class RetroMCP
{
    public readonly string Folder;
    private readonly Sided<Mappings> MatchedMCP;
    private readonly Sided<Mappings> MatchedMojang;
    private readonly VersionedRenames NormalRenames;
    private readonly VersionedRenames CustomRenames;
    public RetroMCP(string folder, string matched_version)
    {
        Folder = folder;
        var mcp = ParseTsrgs(matched_version);
        MatchedMCP = new(mcp.Client.Reversed(), mcp.Server.Reversed());
        MatchedMojang = new();
        using var client_file = File.OpenText(Path.Combine(Folder, "matched_client.txt"));
        using var server_file = File.OpenText(Path.Combine(Folder, "matched_server.txt"));
        MappingsIO.ParseProguard(MatchedMojang.Client, client_file);
        MappingsIO.ParseProguard(MatchedMojang.Server, server_file);
        NormalRenames = new((YamlMappingNode)YamlHelper.ParseFile(Path.Combine(folder, "mappings.yaml")));
        CustomRenames = new((YamlMappingNode)YamlHelper.ParseFile(Path.Combine(folder, "custom.yaml")));
    }

    private Sided<Mappings> ParseTsrgs(string version)
    {
        var sided = new Sided<Mappings>();
        using var client_file = File.OpenText(Path.Combine(Folder, version, "client.tsrg"));
        using var server_file = File.OpenText(Path.Combine(Folder, version, "server.tsrg"));
        MappingsIO.ParseTsrg(sided.Client, client_file);
        MappingsIO.ParseTsrg(sided.Server, server_file);
        return sided;
    }

    public Sided<Mappings> CreateMappings(string version)
    {
        if (!Directory.Exists(Path.Combine(Folder, version)))
            return null;
        var final = new Sided<Mappings>();
        var local = ParseTsrgs(version);
        var sides = new (
            Func<Sided<Mappings>, Mappings> map,
            Func<Sided<FlatMap>, FlatMap> flat,
            Func<VersionedRenames, Func<string, string, string>> class_getter,
            Func<VersionedRenames, Func<string, string, string>> method_getter,
            Func<VersionedRenames, Func<string, string, string>> field_getter
            )[]
        {
            (x => x.Client, x => x.Client, x => x.GetClientClass,  x => x.GetClientMethod, x => x.GetClientField),
             (x => x.Server, x => x.Server, x => x.GetServerClass, x => x.GetServerMethod, x => x.GetServerField)
        };
        foreach (var (map, flat, class_getter, method_getter, field_getter) in sides)
        {
            foreach (var c in map(local).ClassList)
            {
                var matched_mcp = map(MatchedMCP).GetClass(c.NewName);
                MappedClass matched_mojang()
                {
                    if (matched_mcp == null)
                        return null;
                    return map(MatchedMojang).GetClass(matched_mcp.NewName);
                }
                MappedClass mojang = matched_mojang();
                static void WriteText(string text, ConsoleColor color)
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(text);
                    Console.ResetColor();
                }
                MappedClass find_mojang()
                {
                    if (mojang == null)
                        return null;
                    WriteText($"Class {c.OldName}: Mojang Match -> {mojang.NewName}", ConsoleColor.Green);
                    return map(final).AddClass(c.OldName, mojang.NewName);
                }
                MappedClass find_custom(VersionedRenames rename)
                {
                    string new_name = class_getter(rename)(version, c.NewName);
                    if (new_name == null)
                        return null;
                    WriteText($"Class {c.OldName}: Rename Match -> {new_name}", rename == CustomRenames ? ConsoleColor.Cyan : ConsoleColor.Yellow);
                    return map(final).AddClass(c.OldName, new_name);
                }
                MappedClass give_up()
                {
                    WriteText($"Class {c.OldName}: No Match -> {c.NewName}", ConsoleColor.Red);
                    return map(final).AddClass(c.OldName, c.NewName);
                }
                MappedClass final_class = find_mojang() ?? find_custom(CustomRenames) ?? find_custom(NormalRenames) ?? give_up();
                MappedField find_mojang_field(MappedField field)
                {
                    if (mojang == null)
                        return null;
                    var mcp_field = matched_mcp.GetField(field.NewName);
                    if (mcp_field == null)
                        return null;
                    var matched_field = mojang.GetField(mcp_field.NewName);
                    if (matched_field == null)
                        return null;
                    WriteText($"\tField {field.OldName}: Mojang Match -> {matched_field.NewName}", ConsoleColor.Green);
                    return final_class.AddField(field.OldName, matched_field.NewName);
                }
                MappedField find_custom_field(MappedField field, VersionedRenames rename)
                {
                    string new_name = field_getter(rename)(version, field.NewName);
                    if (new_name == null)
                        return null;
                    WriteText($"\tField {field.OldName}: Custom Match -> {new_name}", rename == CustomRenames ? ConsoleColor.Cyan : ConsoleColor.Yellow);
                    return final_class.AddField(field.OldName, new_name);
                }
                MappedField give_up_field(MappedField field)
                {
                    WriteText($"\tField {field.OldName}: No Match -> {field.NewName}", ConsoleColor.Red);
                    return final_class.AddField(field.OldName, field.NewName);
                }
                MappedMethod find_mojang_method(MappedMethod method)
                {
                    if (mojang == null)
                        return null;
                    var mcp_method = matched_mcp.GetMethod(method.NewName, method.Signature);
                    if (mcp_method == null)
                        return null;
                    var matched_method = mojang.GetMethod(mcp_method.NewName, method.Signature);
                    if (matched_method == null)
                        return null;
                    WriteText($"\tMethod {method.OldName}: Mojang Match -> {matched_method.NewName}", ConsoleColor.Green);
                    return final_class.AddMethod(method.OldName, matched_method.NewName, method.Signature);
                }
                MappedMethod find_custom_method(MappedMethod method, VersionedRenames rename)
                {
                    string new_name = method_getter(rename)(version, method.NewName);
                    if (new_name == null)
                        return null;
                    WriteText($"\tMethod {method.OldName}: Custom Match -> {new_name}", rename == CustomRenames ? ConsoleColor.Cyan : ConsoleColor.Yellow);
                    return final_class.AddMethod(method.OldName, new_name, method.Signature);
                }
                MappedMethod give_up_method(MappedMethod method)
                {
                    WriteText($"\tMethod {method.OldName}: No Match -> {method.NewName}", ConsoleColor.Red);
                    return final_class.AddMethod(method.OldName, method.NewName, method.Signature);
                }
                foreach (var item in c.FieldList)
                {
                    MappedField final_field = find_mojang_field(item) ?? find_custom_field(item, CustomRenames) ?? find_custom_field(item, NormalRenames) ?? give_up_field(item);
                }
                foreach (var item in c.MethodList)
                {
                    MappedMethod final_method = find_mojang_method(item) ?? find_custom_method(item, CustomRenames) ?? find_custom_method(item, NormalRenames) ?? give_up_method(item);
                }
            }
        }
        return final;
    }
}
