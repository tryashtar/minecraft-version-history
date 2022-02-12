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

    public void WriteTo(string file)
    {
        var root = new YamlSequenceNode();
        foreach (var (spec, renames) in Renames)
        {
            var node = new YamlMappingNode();
            node.Add("affects", spec.Serialize());
            var mappings = new YamlMappingNode();
            node.Add("mappings", mappings);
            root.Add(node);
            var sides = new[] { ("client", renames.Client), ("server", renames.Server) };
            foreach (var (name, map) in sides)
            {
                var side_node = new YamlMappingNode();
                node.Add(name, side_node);
                var classes = new YamlMappingNode();
                var fields = new YamlMappingNode();
                var methods = new YamlMappingNode();
                side_node.Add("classes", classes);
                side_node.Add("fields", fields);
                side_node.Add("methods", methods);
                classes.Add("equivalencies", SerializeEquivalencies(map.ClassEquivalencies));
                fields.Add("equivalencies", SerializeEquivalencies(map.FieldEquivalencies));
                methods.Add("equivalencies", SerializeEquivalencies(map.MethodEquivalencies));
                classes.Add("mappings", SerializeMappings(map.ClassMap));
                fields.Add("mappings", SerializeMappings(map.FieldMap));
                methods.Add("mappings", SerializeMappings(map.MethodMap));
            }
        }
        YamlHelper.SaveToFile(root, file);
    }

    private YamlMappingNode SerializeMappings(IEnumerable<Rename> renames)
    {
        var node = new YamlMappingNode();
        foreach (var item in renames)
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