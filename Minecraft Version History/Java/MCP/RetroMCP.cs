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
        void find_matches<T, U>(
            T input,
            Func<Mappings, U> prepare_mcp,
            Func<Mappings, U> prepare_moj,
            Func<U, string, T> get,
            Action<U, string, string> add,
            Func<FlatMap, string, string> get_flat,
            // when your code is so convoluted it requires rank 2 polymorphism
            Func<Sided<Mappings>, Mappings> map_side,
            Func<Sided<FlatMap>, FlatMap> flat_side,
            Func<T, string> old_name,
            Func<T, string> new_name) where T : class
        {
            var matched_local = get(prepare_mcp(map_side(MatchedMCP)), new_name(input));
            if (matched_local != null)
            {
                var matched_mojang = get(prepare_moj(map_side(MatchedMojang)), new_name(matched_local));
                add(prepare(map_side(final)), old_name(input), new_name(matched_mojang));
#if DEBUG
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Mojang match! {new_name(input)} -> {new_name(matched_mojang)}");
                Console.ResetColor();
#endif
            }
            else
            {
                // couldn't find a match in 1.14, try custom overrides
                bool found = false;
                foreach (var subs in CustomRenames.Where(x => x.applies(version)).Select(x => x.renames))
                {
                    var rename = get_flat(flat_side(subs), new_name(input));
                    if (rename != null)
                    {
#if DEBUG
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"Custom match! {new_name(input)} -> {rename}");
                        Console.ResetColor();
#endif
                        add(prepare(map_side(final)), old_name(input), rename);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    // try local friendlies
                    var friendly = get_flat(flat_side(friendlies), new_name(input));
                    if (friendly != null)
                    {
#if DEBUG
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Local match! {new_name(input)} -> {friendly}");
                        Console.ResetColor();
#endif
                        add(prepare(map_side(final)), old_name(input), friendly);
                    }
                    else
                    {
                        // nothing
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Unable to find a friendly name for {new_name(input)}");
                        Console.ResetColor();
                        add(prepare(map_side(final)), old_name(input), new_name(input));
                    }
                }
            }
        }
        var sides = new (Func<Sided<Mappings>, Mappings> map, Func<Sided<FlatMap>, FlatMap> flat)[]
        {
            (x => x.Client, x => x.Client),
            (x => x.Server, x => x.Server)
        };
        foreach (var (map, flat) in sides)
        {
            foreach (var c in map(local).ClassList)
            {
                find_matches(
                    input: c,
                    prepare: x => x,
                    get: (x, y) => x.GetClass(y),
                    add: (x, y, z) => x.AddClass(y, z),
                    get_flat: (x, y) => x.GetClass(y),
                    map_side: map,
                    flat_side: flat,
                    old_name: x => x.OldName,
                    new_name: x => x.NewName
                );
                foreach (var f in c.FieldList)
                {
                    find_matches(
                        input: f,
                        prepare: x => x.GetClass(c.NewName),
                        get: (x, y) => x.GetField(y),
                        add: (x, y, z) => x.AddField(y, z),
                        get_flat: (x, y) => x.GetField(y),
                        map_side: map,
                        flat_side: flat,
                        old_name: x => x.OldName,
                        new_name: x => x.NewName
                    );
                }
                foreach (var m in c.MethodList)
                {
                    find_matches(
                        input: m,
                        prepare: x => x.GetClass(c.NewName),
                        get: (x, y) => x.GetMethod(y, m.Signature),
                        add: (x, y, z) => x.AddMethod(y, z, m.Signature),
                        get_flat: (x, y) => x.GetMethod(y),
                        map_side: map,
                        flat_side: flat,
                        old_name: x => x.OldName,
                        new_name: x => x.NewName
                    );
                }
            }
        }
        return final;
    }
}
