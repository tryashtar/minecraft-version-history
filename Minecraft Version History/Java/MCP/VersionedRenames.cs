namespace MinecraftVersionHistory;

public class VersionedRenames
{
    private readonly List<(VersionSpec spec, Sided<FlatMap> renames)> Renames;
    public VersionedRenames(YamlSequenceNode sequence)
    {
        Renames = sequence.OfType<YamlMappingNode>().Select(ParseNode).ToList();
    }

    public VersionedRenames()
    {
        Renames = new();
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
                add_flat(side, x => x.AddEquivalentClasses(c));
            }
            foreach (var f in field_eq)
            {
                add_flat(side, x => x.AddEquivalentMethods(f));
            }
            foreach (var m in method_eq)
            {
                add_flat(side, x => x.AddEquivalentMethods(m));
            }
        }
        return (spec, renames);
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
        void send2(Func<FlatMap, IEnumerable<IEnumerable<string>>> getter, Action<FlatMap, IEnumerable<string>> adder)
        {
            var client_items = getter(map.Client).Select(x => x.ToHashSet()).ToList();
            var server_items = getter(map.Server).Select(x => x.ToHashSet()).ToList();
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
        send(x => x.ClassMap, (x, y, z) => x.AddClass(y, z));
        send(x => x.MethodMap, (x, y, z) => x.AddMethod(y, z));
        send(x => x.FieldMap, (x, y, z) => x.AddField(y, z));
        send2(x => x.ClassEquivalencies, (x, y) => x.AddEquivalentClasses(y));
        send2(x => x.MethodEquivalencies, (x, y) => x.AddEquivalentMethods(y));
        send2(x => x.FieldEquivalencies, (x, y) => x.AddEquivalentFields(y));
        return (client, server, joined);
    }

    public void WriteTo(string file)
    {
        static void add_node(YamlMappingNode node, string key, YamlNode value)
        {
            if (value is YamlScalarNode || (value is YamlSequenceNode seq && seq.Children.Count > 0) || (value is YamlMappingNode map && map.Children.Count > 0))
                node.Add(key, value);
        }
        var root = new YamlSequenceNode();
        foreach (var (spec, renames) in Renames)
        {
            var node = new YamlMappingNode();
            node.Add("affects", spec.Serialize());
            root.Add(node);
            var mappings = new YamlMappingNode();
            var (client, server, joined) = Split(renames);
            var sides = new[] { ("client", client), ("server", server), ("joined", joined) };
            foreach (var (name, map) in sides)
            {
                var side_node = new YamlMappingNode();
                var classes = new YamlMappingNode();
                var fields = new YamlMappingNode();
                var methods = new YamlMappingNode();
                add_node(classes, "equivalencies", SerializeEquivalencies(map.ClassEquivalencies));
                add_node(fields, "equivalencies", SerializeEquivalencies(map.FieldEquivalencies));
                add_node(methods, "equivalencies", SerializeEquivalencies(map.MethodEquivalencies));
                add_node(classes, "mappings", SerializeMappings(map.ClassMap));
                add_node(fields, "mappings", SerializeMappings(map.FieldMap));
                add_node(methods, "mappings", SerializeMappings(map.MethodMap));
                add_node(side_node, "classes", classes);
                add_node(side_node, "fields", fields);
                add_node(side_node, "methods", methods);
                add_node(mappings, name, side_node);
            }
            add_node(node, "mappings", mappings);
        }
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

    private YamlSequenceNode SerializeEquivalencies(IEnumerable<IEnumerable<string>> eq)
    {
        var node = new YamlSequenceNode();
        foreach (var list in eq)
        {
            var child = new YamlSequenceNode();
            foreach (var entry in list)
            {
                child.Add(entry);
            }
            node.Add(child);
        }
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