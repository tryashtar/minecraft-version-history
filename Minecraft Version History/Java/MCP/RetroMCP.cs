namespace MinecraftVersionHistory;

public class RetroMCP
{
    public readonly string Folder;
    private readonly Sided<Mappings> MatchedMCP;
    private readonly Sided<Mappings> MatchedMojang;
    private readonly List<CustomRename> CustomRenames = new();
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
            CustomRenames.Add(new CustomRename((YamlMappingNode)item));
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
        if (!Directory.Exists(Path.Combine(Folder, version)))
            return null;
        var final = new Sided<Mappings>();
        var local = ParseTsrgs(version);
        var friendlies = ParseCSVs(version);
        var renames = CustomRenames.Where(x => x.AppliesTo(version)).ToList();
        T get_with_equivalencies<T>(string name, Func<CustomRename, HashSet<string>> equiv_getter, Func<string, T> getter)
        {
            var result = getter(name);
            foreach (var rename in renames)
            {
                var equiv = equiv_getter(rename);
                if (equiv.Contains(name))
                {
                    foreach (var same in equiv)
                    {
                        var new_result = getter(same);
                        if (result != null && new_result != null)
                            throw new InvalidOperationException($"How are {result} and {new_result} equivalent?");
                        result = new_result;
                    }
                }
            }
            return result;
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
                MappedClass final_class = null;
                MappedClass matched_mojang = null;
                MappedClass matched_mcp = null;
                matched_mcp = map(MatchedMCP).GetClass(c.NewName);
                if (matched_mcp != null)
                {
                    matched_mojang = map(MatchedMojang).GetClass(matched_mcp.NewName);
                    // should only be null if this is one-sided
                    if (matched_mojang != null)
                    {
#if DEBUG
                        //Console.ForegroundColor = ConsoleColor.Green;
                        //Console.WriteLine($"Mojang match! {c.NewName} -> {matched_mojang.NewName}");
                        //Console.ResetColor();
#endif
                        final_class = map(final).AddClass(c.OldName, matched_mojang.NewName);
                    }
                    else
                        continue;
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
                            found_field = true;
                            var mojang_field = matched_mojang.GetField(local_field.NewName);
                            // should only be null if this is one-sided
                            if (mojang_field != null)
                            {
#if DEBUG
                                ///Console.ForegroundColor = ConsoleColor.Green;
                                ///Console.WriteLine($"\tMojang match! {f.NewName} -> {mojang_field.NewName}");
                                ///Console.ResetColor();
#endif
                                final_class.AddField(f.OldName, mojang_field.NewName);
                            }
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
                            Console.WriteLine($"\tUnable to find a friendly name for {c.NewName}.{f.NewName}");
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
                            found_method = true;
                            var mojang_method = matched_mojang.GetMethod(local_method.NewName, m.Signature);
                            // should only be null if this is one-sided
                            if (mojang_method != null)
                            {
#if DEBUG
                                //Console.ForegroundColor = ConsoleColor.Green;
                                //Console.WriteLine($"\tMojang match! {m.NewName} -> {mojang_method.NewName}");
                                //Console.ResetColor();
#endif
                                final_class.AddMethod(m.OldName, mojang_method.NewName, m.Signature);
                            }
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
                            Console.WriteLine($"\tUnable to find a friendly name for {c.NewName}.{m.NewName}");
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

public class CustomRename
{
    private readonly Predicate<string> VersionPredicate;
    public readonly Sided<List<HashSet<string>>> EquivalentClasses = new();
    public readonly Sided<List<HashSet<string>>> EquivalentFields = new();
    public readonly Sided<List<HashSet<string>>> EquivalentMethods = new();
    public readonly Sided<FlatMap> Renames = new();
    public CustomRename(YamlMappingNode node)
    {
        var affects = node.Go("affects");
        if (affects is YamlScalarNode n && n.Value == "*")
            VersionPredicate = x => true;
        else if (affects is YamlSequenceNode s)
            VersionPredicate = x => s.ToStringList().Contains(x);
        void add_flat(string side, Action<FlatMap> adder)
        {
            if (side == "client" || side == "joined")
                adder(Renames.Client);
            if (side == "server" || side == "joined")
                adder(Renames.Server);
        }
        void add_set(string side, Sided<List<HashSet<string>>> set, Action<List<HashSet<string>>> adder)
        {
            if (side == "client" || side == "joined")
                adder(set.Client);
            if (side == "server" || side == "joined")
                adder(set.Server);
        }
        foreach (var side in new[] { "client", "server", "joined" })
        {
            var class_renames = node.Go("mappings", side, "classes", "renames").ToDictionary() ?? new();
            var field_renames = node.Go("mappings", side, "fields", "renames").ToDictionary() ?? new();
            var method_renames = node.Go("mappings", side, "methods", "renames").ToDictionary() ?? new();
            foreach (var c in class_renames)
            {
                add_flat(side, x => x.AddClass(c.Key, c.Value));
            }
            foreach (var f in field_renames)
            {
                add_flat(side, x => x.AddField(f.Key, f.Value));
            }
            foreach (var m in method_renames)
            {
                add_flat(side, x => x.AddMethod(m.Key, m.Value));
            }
            var class_eq = node.Go("mappings", side, "classes", "equivalencies").ToList(x => x.ToStringList());
            var field_eq = node.Go("mappings", side, "fields", "equivalencies").ToList(x => x.ToStringList());
            var method_eq = node.Go("mappings", side, "methods", "equivalencies").ToList(x => x.ToStringList());
            foreach (var c in class_eq)
            {
                add_set(side, EquivalentClasses, x => x.Add(c.ToHashSet()));
            }
            foreach (var f in field_eq)
            {
                add_set(side, EquivalentFields, x => x.Add(f.ToHashSet()));
            }
            foreach (var m in method_eq)
            {
                add_set(side, EquivalentMethods, x => x.Add(m.ToHashSet()));
            }
        }
    }

    public HashSet<string> GetEquivalencies(string name)
    {
        foreach (var set in EquivalentClasses.Server)
        {
            if (set.Contains(name))
                return set;
        }
        return new HashSet<string> { name };
    }

    public bool AppliesTo(string version)
    {
        return VersionPredicate(version);
    }
}
