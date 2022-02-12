namespace MinecraftVersionHistory;

public class VersionedRenames
{
    private readonly List<(VersionSpec spec, Sided<FlatMap> renames)> Renames;
    public readonly Sided<Equivalencies> Equivalencies;
    public VersionedRenames(YamlMappingNode node)
    {
        Renames = ((YamlSequenceNode)node.Go("mappings")).OfType<YamlMappingNode>().Select(ParseNode).ToList();
        Equivalencies = new();
        foreach (var side in new[] { "client", "server", "joined" })
        {
            var classes = node.Go("equivalencies", side, "classes").NullableParse(x => ParseEquivalencies((YamlSequenceNode)x)) ?? new();
            var fields = node.Go("equivalencies", side, "fields").NullableParse(x => ParseEquivalencies((YamlSequenceNode)x)) ?? new();
            var methods = node.Go("equivalencies", side, "methods").NullableParse(x => ParseEquivalencies((YamlSequenceNode)x)) ?? new();
            if (side == "client" || side == "joined")
            {
                foreach (var item in classes)
                {
                    Equivalencies.Client.AddClasses(item);
                }
                foreach (var item in fields)
                {
                    Equivalencies.Client.AddFields(item);
                }
                foreach (var item in methods)
                {
                    Equivalencies.Client.AddMethods(item);
                }
            }
            if (side == "server" || side == "joined")
            {
                foreach (var item in classes)
                {
                    Equivalencies.Server.AddClasses(item);
                }
                foreach (var item in fields)
                {
                    Equivalencies.Server.AddFields(item);
                }
                foreach (var item in methods)
                {
                    Equivalencies.Server.AddMethods(item);
                }
            }
        }
    }

    public VersionedRenames()
    {
        Renames = new();
        Equivalencies = new();
    }

    private List<HashSet<string>> ParseEquivalencies(YamlSequenceNode node)
    {
        var list = new List<HashSet<string>>();
        foreach (YamlSequenceNode item in node)
        {
            var set = new HashSet<string>(item.Children.Select(x => x.String()));
            list.Add(set);
        }
        return list;
    }

    public void Add(VersionSpec spec, Sided<FlatMap> renames)
    {
        Renames.Add((spec, renames));
    }

    private (VersionSpec spec, Sided<FlatMap> renames) ParseNode(YamlMappingNode node)
    {
        VersionSpec spec = new(node.Go("affects"));
        Sided<FlatMap> renames = new();
        void add_flat(string side, Action<FlatMap> adder)
        {
            if (side == "client" || side == "joined")
                adder(renames.Client);
            if (side == "server" || side == "joined")
                adder(renames.Server);
        }
        foreach (var side in new[] { "client", "server", "joined" })
        {
            var class_renames = node.Go("mappings", side, "classes").ToDictionary() ?? new();
            var field_renames = node.Go("mappings", side, "fields").ToDictionary() ?? new();
            var method_renames = node.Go("mappings", side, "methods").ToDictionary() ?? new();
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
        }
        return (spec, renames);
    }

    private (Equivalencies client, Equivalencies server, Equivalencies joined) Split(Sided<Equivalencies> sided)
    {
        var client = new Equivalencies();
        var server = new Equivalencies();
        var joined = new Equivalencies();
        void send(Func<Equivalencies, IEnumerable<IEnumerable<string>>> getter, Action<Equivalencies, IEnumerable<string>> adder)
        {
            var client_items = getter(sided.Client).Select(x => x.ToHashSet()).ToList();
            var server_items = getter(sided.Server).Select(x => x.ToHashSet()).ToList();
            var comparer = HashSet<string>.CreateSetComparer();
            foreach (var item in client_items.Intersect(server_items, comparer))
            {
                adder(joined, item);
            }
            foreach (var item in client_items.Except(server_items, comparer))
            {
                adder(server, item);
            }
            foreach (var item in server_items.Except(client_items, comparer))
            {
                adder(client, item);
            }
        }
        send(x => x.Classes, (x, y) => x.AddClasses(y));
        send(x => x.Methods, (x, y) => x.AddMethods(y));
        send(x => x.Fields, (x, y) => x.AddFields(y));
        return (client, server, joined);
    }

    private (FlatMap client, FlatMap server, FlatMap joined) Split(Sided<FlatMap> map)
    {
        var client = new FlatMap();
        var server = new FlatMap();
        var joined = new FlatMap();
        void send(Func<FlatMap, IEnumerable<Rename>> getter, Action<FlatMap, string, string> adder)
        {
            var client_items = getter(map.Client).ToHashSet();
            var server_items = getter(map.Server).ToHashSet();
            foreach (var item in client_items.Intersect(server_items))
            {
                adder(joined, item.OldName, item.NewName);
            }
            foreach (var item in client_items.Except(server_items))
            {
                adder(server, item.OldName, item.NewName);
            }
            foreach (var item in server_items.Except(client_items))
            {
                adder(client, item.OldName, item.NewName);
            }
        }
        send(x => x.ClassMap, (x, y, z) => x.AddClass(y, z));
        send(x => x.MethodMap, (x, y, z) => x.AddMethod(y, z));
        send(x => x.FieldMap, (x, y, z) => x.AddField(y, z));
        return (client, server, joined);
    }

    private void AddIfPresent(YamlMappingNode node, string key, YamlNode value)
    {
        if (value is YamlScalarNode || (value is YamlSequenceNode seq && seq.Children.Count > 0) || (value is YamlMappingNode map && map.Children.Count > 0))
            node.Add(key, value);
    }

    public void WriteTo(string file)
    {
        var root = new YamlMappingNode();
        var list = new YamlSequenceNode();
        foreach (var (spec, renames) in Renames)
        {
            var node = new YamlMappingNode();
            node.Add("affects", spec.Serialize());
            list.Add(node);
            var mappings = new YamlMappingNode();
            var (client, server, joined) = Split(renames);
            var sides = new[] { ("client", client), ("server", server), ("joined", joined) };
            foreach (var (name, map) in sides)
            {
                var side_node = new YamlMappingNode();
                AddIfPresent(side_node, "classes", SerializeMappings(map.ClassMap));
                AddIfPresent(side_node, "fields", SerializeMappings(map.FieldMap));
                AddIfPresent(side_node, "methods", SerializeMappings(map.MethodMap));
                AddIfPresent(mappings, name, side_node);
            }
            AddIfPresent(node, "mappings", mappings);
        }
        AddIfPresent(root, "mappings", list);
        var equivs = new YamlMappingNode();
        var (eclient, eserver, ejoined) = Split(Equivalencies);
        var esides = new[] { ("client", eclient), ("server", eserver), ("joined", ejoined) };
        foreach (var (name, eq) in esides)
        {
            AddIfPresent(equivs, name, SerializeEquivalencies(eq));
        }
        AddIfPresent(root, "equivalencies", equivs);
        YamlHelper.SaveToFile(root, file);
    }

    private class RenameSorter : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            string[] x_scores = x.Split('_');
            string[] y_scores = y.Split('_');
            for (int i = 0; i < Math.Min(x_scores.Length, y_scores.Length); i++)
            {
                if (int.TryParse(x_scores[i], out int xi) && int.TryParse(y_scores[i], out int yi))
                {
                    int compare = xi.CompareTo(yi);
                    if (compare != 0)
                        return compare;
                }
                int compare_str = x_scores[i].CompareTo(y_scores[i]);
                if (compare_str != 0)
                    return compare_str;
            }
            return x_scores.Length.CompareTo(y_scores.Length);
        }
    }

    private YamlMappingNode SerializeMappings(IEnumerable<Rename> renames)
    {
        var node = new YamlMappingNode();
        foreach (var item in renames.OrderBy(x => x.OldName, new RenameSorter()))
        {
            node.Add(item.OldName, item.NewName);
        }
        return node;
    }

    private YamlMappingNode SerializeEquivalencies(Equivalencies eq)
    {
        var node = new YamlMappingNode();
        var classes = new YamlSequenceNode();
        var methods = new YamlSequenceNode();
        var fields = new YamlSequenceNode();
        foreach (var item in eq.Classes)
        {
            var sub = new YamlSequenceNode();
            foreach (var i in item)
            {
                sub.Add(i);
            }
            classes.Add(sub);
        }
        foreach (var item in eq.Methods)
        {
            var sub = new YamlSequenceNode();
            foreach (var i in item)
            {
                sub.Add(i);
            }
            methods.Add(sub);
        }
        foreach (var item in eq.Fields)
        {
            var sub = new YamlSequenceNode();
            foreach (var i in item)
            {
                sub.Add(i);
            }
            fields.Add(sub);
        }
        AddIfPresent(node, "classes", classes);
        AddIfPresent(node, "methods", methods);
        AddIfPresent(node, "fields", fields);
        return node;
    }
}

public class VersionSpec
{
    private readonly bool AllVersions = false;
    private readonly List<string> Accepted = new();

    public VersionSpec(string version)
    {
        Accepted.Add(version);
    }

    public VersionSpec(IEnumerable<string> versions)
    {
        Accepted.AddRange(versions);
    }

    private VersionSpec(bool all)
    {
        AllVersions = all;
    }

    public static VersionSpec All => new VersionSpec(true);

    public VersionSpec(YamlNode node)
    {
        if (node is YamlScalarNode n)
        {
            string val = n.Value;
            if (val == "*")
                AllVersions = true;
            else
                Accepted.Add(val);
        }
        else if (node is YamlSequenceNode s)
            Accepted.AddRange(s.ToStringList());
    }

    public YamlNode Serialize()
    {
        if (AllVersions)
            return new YamlScalarNode("*");
        if (Accepted.Count == 1)
            return new YamlScalarNode(Accepted[0]);
        return new YamlSequenceNode(Accepted.Select(x => new YamlScalarNode(x)));
    }

    public bool AppliesTo(string version)
    {
        return AllVersions || Accepted.Contains(version);
    }
}