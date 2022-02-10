namespace MinecraftVersionHistory;

public class RetroMCP
{
    public readonly string Folder;
    private readonly Sided<Mappings> MatchedMCP;
    private readonly Sided<Mappings> MatchedMojang;
    private readonly List<(Predicate<string> applies, Sided<FlatMap> renames)> CustomRenames = new();
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
        var list = (YamlSequenceNode)YamlHelper.ParseFile(Path.Combine(folder, "mappings.yaml"));
        foreach (var item in list)
        {
            Predicate<string> pred = null;
            var affects = item.Go("affects");
            if (affects is YamlScalarNode n && n.Value == "*")
                pred = x => true;
            else if (affects is YamlSequenceNode s)
                pred = x => s.ToStringList().Contains(x);
            var map = new Sided<FlatMap>();
            void add(string side, Action<FlatMap> adder)
            {
                if (side == "client" || side == "joined")
                    adder(map.Client);
                if (side == "server" || side == "joined")
                    adder(map.Server);
            }
            foreach (var side in new[] { "client", "server", "joined" })
            {
                var classes = item.Go("mappings", side, "classes").ToDictionary() ?? new();
                var fields = item.Go("mappings", side, "fields").ToDictionary() ?? new();
                var methods = item.Go("mappings", side, "methods").ToDictionary() ?? new();
                foreach (var c in classes)
                {
                    add(side, x => x.AddClass(c.Key, c.Value));
                }
                foreach (var f in fields)
                {
                    add(side, x => x.AddField(f.Key, f.Value));
                }
                foreach (var m in methods)
                {
                    add(side, x => x.AddMethod(m.Key, m.Value));
                }
            }
            CustomRenames.Add((pred, map));
        }
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

    private Sided<FlatMap> ParseCSVs(string version)
    {
        var sided = new Sided<FlatMap>();
        using var client_fields = File.OpenText(Path.Combine(Folder, version, "client_fields.csv"));
        using var client_methods = File.OpenText(Path.Combine(Folder, version, "client_methods.csv"));
        using var server_fields = File.OpenText(Path.Combine(Folder, version, "server_fields.csv"));
        using var server_methods = File.OpenText(Path.Combine(Folder, version, "server_methods.csv"));
        MappingsIO.ParseCSVs(sided.Client, client_fields, client_methods);
        MappingsIO.ParseCSVs(sided.Server, server_fields, server_methods);
        return sided;
    }

    public Sided<Mappings> CreateMappings(string version)
    {
        var final = new Sided<Mappings>();
        var local = ParseTsrgs(version);
        var friendlies = ParseCSVs(version);
        var renames = CustomRenames.Where(x => x.applies(version)).Select(x => x.renames).ToList();
        var sides = new (Func<Sided<Mappings>, Mappings> map, Func<Sided<FlatMap>, FlatMap> flat)[]
        {
             (x => x.Client, x => x.Client),
             (x => x.Server, x => x.Server)
        };
        foreach (var (map, flat) in sides)
        {
            foreach (var c in map(local).ClassList)
            {
                MappedClass final_class = null;
                MappedClass matched_mojang = null;
                MappedClass matched_mcp = null;
                matched_mcp = map(MatchedMCP).GetClass(c.NewName);
                if (matched_mcp != null)
                {
                    matched_mojang = map(MatchedMojang).GetClass(matched_mcp.NewName);
#if DEBUG
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Mojang match! {c.NewName} -> {matched_mojang.NewName}");
                    Console.ResetColor();
#endif
                    final_class = map(final).AddClass(c.OldName, matched_mojang.NewName);
                }
                else
                {
                    // couldn't find a match, try custom overrides
                    bool found = false;
                    foreach (var subs in renames)
                    {
                        var rename = flat(subs).GetClass(c.NewName);
                        if (rename != null)
                        {
#if DEBUG
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"Custom match! {c.NewName} -> {rename}");
                            Console.ResetColor();
#endif
                            final_class = map(final).AddClass(c.OldName, rename);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        // try local friendlies
                        var friendly = flat(friendlies).GetClass(c.NewName);
                        if (friendly != null)
                        {
#if DEBUG
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Local match! {c.NewName} -> {friendly}");
                            Console.ResetColor();
#endif
                            final_class = map(final).AddClass(c.OldName, friendly);
                        }
                        else
                        {
                            // nothing
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Unable to find a friendly name for {c.NewName}");
                            Console.ResetColor();
                            final_class = map(final).AddClass(c.OldName, c.NewName);
                        }
                    }
                }
                foreach (var f in c.FieldList)
                {
                    bool found_field = false;
                    if (matched_mcp != null)
                    {
                        var local_field = matched_mcp.GetField(f.NewName);
                        if (local_field != null)
                        {
                            var mojang_field = matched_mojang.GetField(local_field.NewName);
#if DEBUG
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\tMojang match! {f.NewName} -> {mojang_field.NewName}");
                            Console.ResetColor();
#endif
                            found_field = true;
                            final_class.AddField(f.OldName, mojang_field.NewName);
                        }
                    }
                    if (!found_field)
                    {
                        // couldn't find a match, try custom overrides
                        foreach (var subs in renames)
                        {
                            var rename = flat(subs).GetField(f.NewName);
                            if (rename != null)
                            {
#if DEBUG
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"\tCustom match! {f.NewName} -> {rename}");
                                Console.ResetColor();
#endif
                                final_class.AddField(f.OldName, rename);
                                found_field = true;
                                break;
                            }
                        }
                    }
                    if (!found_field)
                    {
                        // try local friendlies
                        var friendly = flat(friendlies).GetField(f.NewName);
                        if (friendly != null)
                        {
#if DEBUG
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"\tLocal match! {f.NewName} -> {friendly}");
                            Console.ResetColor();
#endif
                            final_class.AddField(f.OldName, friendly);
                        }
                        else
                        {
                            // nothing
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\tUnable to find a friendly name for {f.NewName}");
                            Console.ResetColor();
                            final_class.AddField(f.OldName, f.NewName);
                        }
                    }
                }
                foreach (var m in c.MethodList)
                {
                    bool found_method = false;
                    if (matched_mcp != null)
                    {
                        var local_method = matched_mcp.GetMethod(m.NewName, m.Signature);
                        if (local_method != null)
                        {
                            var mojang_method = matched_mojang.GetMethod(local_method.NewName, m.Signature);
#if DEBUG
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\tMojang match! {m.NewName} -> {mojang_method.NewName}");
                            Console.ResetColor();
#endif
                            found_method = true;
                            final_class.AddMethod(m.OldName, mojang_method.NewName, m.Signature);
                        }
                    }
                    if (!found_method)
                    {
                        // couldn't find a match, try custom overrides
                        foreach (var subs in renames)
                        {
                            var rename = flat(subs).GetMethod(m.NewName);
                            if (rename != null)
                            {
#if DEBUG
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"\tCustom match! {m.NewName} -> {rename}");
                                Console.ResetColor();
#endif
                                final_class.AddMethod(m.OldName, rename, m.Signature);
                                found_method = true;
                                break;
                            }
                        }
                    }
                    if (!found_method)
                    {
                        // try local friendlies
                        var friendly = flat(friendlies).GetMethod(m.NewName);
                        if (friendly != null)
                        {
#if DEBUG
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"\tLocal match! {m.NewName} -> {friendly}");
                            Console.ResetColor();
#endif
                            final_class.AddMethod(m.OldName, friendly, m.Signature);
                        }
                        else
                        {
                            // nothing
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\tUnable to find a friendly name for {m.NewName}");
                            Console.ResetColor();
                            final_class.AddMethod(m.OldName, m.NewName, m.Signature);
                        }
                    }
                }
            }
        }
        return final;
    }
}
