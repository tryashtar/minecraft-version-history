namespace MinecraftVersionHistory;

public class RetroMCP
{
    public readonly string Folder;
    private readonly Sided<Mappings> MatchedMCP;
    private readonly Sided<Mappings> MatchedMojang;
    private readonly VersionedRenames FoundRenames;
    private readonly VersionedRenames CustomRenames;
    private readonly Sided<Equivalencies> MergedEquivalencies;
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
        FoundRenames = new((YamlMappingNode)YamlHelper.ParseFile(Path.Combine(folder, "mappings_found.yaml")));
        CustomRenames = new((YamlMappingNode)YamlHelper.ParseFile(Path.Combine(folder, "mappings_custom.yaml")));
        var found_equivs = Equivalencies.Parse((YamlMappingNode)YamlHelper.ParseFile(Path.Combine(folder, "equivalencies_custom.yaml")));
        var custom_equivs = Equivalencies.Parse((YamlMappingNode)YamlHelper.ParseFile(Path.Combine(folder, "equivalencies_custom.yaml")));
        var client_equivs = new Equivalencies(found_equivs.Client, custom_equivs.Client);
        var server_equivs = new Equivalencies(found_equivs.Server, custom_equivs.Server);
        MergedEquivalencies = new(client_equivs, server_equivs);
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
            Equivalencies eq
            )[]
        {
            (x => x.Client, x => x.Client, MergedEquivalencies.Client),
            (x => x.Server, x => x.Server, MergedEquivalencies.Server)
        };
        foreach (var (map, flat, eq) in sides)
        {
            foreach (var c in map(local).ClassList)
            {
                var matched_mcp = map(MatchedMCP).GetClass(c.NewName, eq);
                MappedClass matched_mojang()
                {
                    if (matched_mcp == null)
                        return null;
                    return map(MatchedMojang).GetClass(matched_mcp.NewName, eq);
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
                    string new_name = rename.GetClass(version, c.NewName, eq);
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
                MappedClass final_class = find_mojang() ?? find_custom(CustomRenames) ?? find_custom(FoundRenames) ?? give_up();
                MappedField find_mojang_field(MappedField field)
                {
                    if (mojang == null)
                        return null;
                    var mcp_field = matched_mcp.GetField(field.NewName, eq);
                    if (mcp_field == null)
                        return null;
                    var matched_field = mojang.GetField(mcp_field.NewName, eq);
                    if (matched_field == null)
                        return null;
                    WriteText($"\tField {field.OldName}: Mojang Match -> {matched_field.NewName}", ConsoleColor.Green);
                    return final_class.AddField(field.OldName, matched_field.NewName);
                }
                MappedField find_custom_field(MappedField field, VersionedRenames rename)
                {
                    string new_name = rename.GetField(version, field.NewName, eq);
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
                    var mcp_method = matched_mcp.GetMethod(method.NewName, method.Signature, eq);
                    if (mcp_method == null)
                        return null;
                    var matched_method = mojang.GetMethod(mcp_method.NewName, method.Signature, eq);
                    if (matched_method == null)
                        return null;
                    WriteText($"\tMethod {method.OldName}: Mojang Match -> {matched_method.NewName}", ConsoleColor.Green);
                    return final_class.AddMethod(method.OldName, matched_method.NewName, method.Signature);
                }
                MappedMethod find_custom_method(MappedMethod method, VersionedRenames rename)
                {
                    string new_name = rename.GetMethod(version, method.NewName, eq);
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
                    MappedField final_field = find_mojang_field(item) ?? find_custom_field(item, CustomRenames) ?? find_custom_field(item, FoundRenames) ?? give_up_field(item);
                }
                foreach (var item in c.MethodList)
                {
                    MappedMethod final_method = find_mojang_method(item) ?? find_custom_method(item, CustomRenames) ?? find_custom_method(item, FoundRenames) ?? give_up_method(item);
                }
            }
        }
        return final;
    }
}
