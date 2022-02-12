namespace MinecraftVersionHistory;

public class Equivalencies
{
    private readonly List<HashSet<string>> EquivalentClasses;
    private readonly List<HashSet<string>> EquivalentFields;
    private readonly List<HashSet<string>> EquivalentMethods;
    public IEnumerable<IEnumerable<string>> Classes => EquivalentClasses;
    public IEnumerable<IEnumerable<string>> Fields => EquivalentFields;
    public IEnumerable<IEnumerable<string>> Methods => EquivalentMethods;

    public static Sided<Equivalencies> Parse(YamlMappingNode node)
    {
        var eq = new Sided<Equivalencies>();
        foreach (var side in new[] { "client", "server", "joined" })
        {
            var classes = node.Go("equivalencies", side, "classes").NullableParse(x => ParseEquivalencies((YamlSequenceNode)x)) ?? new();
            var fields = node.Go("equivalencies", side, "fields").NullableParse(x => ParseEquivalencies((YamlSequenceNode)x)) ?? new();
            var methods = node.Go("equivalencies", side, "methods").NullableParse(x => ParseEquivalencies((YamlSequenceNode)x)) ?? new();
            if (side == "client" || side == "joined")
            {
                foreach (var item in classes)
                {
                    eq.Client.AddClasses(item);
                }
                foreach (var item in fields)
                {
                    eq.Client.AddFields(item);
                }
                foreach (var item in methods)
                {
                    eq.Client.AddMethods(item);
                }
            }
            if (side == "server" || side == "joined")
            {
                foreach (var item in classes)
                {
                    eq.Server.AddClasses(item);
                }
                foreach (var item in fields)
                {
                    eq.Server.AddFields(item);
                }
                foreach (var item in methods)
                {
                    eq.Server.AddMethods(item);
                }
            }
        }
        return eq;
    }

    public static void WriteTo(Sided<Equivalencies> equivs, string file)
    {
        var root = new YamlMappingNode();
        var equiv_node = new YamlMappingNode();
        var (eclient, eserver, ejoined) = Split(equivs);
        var esides = new[] { ("client", eclient), ("server", eserver), ("joined", ejoined) };
        foreach (var (name, eq) in esides)
        {
            AddIfPresent(equiv_node, name, SerializeEquivalencies(eq));
        }
        AddIfPresent(root, "equivalencies", equiv_node);
        YamlHelper.SaveToFile(root, file);
    }

    private static YamlMappingNode SerializeEquivalencies(Equivalencies eq)
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

    private static void AddIfPresent(YamlMappingNode node, string key, YamlNode value)
    {
        if (value is YamlScalarNode || (value is YamlSequenceNode seq && seq.Children.Count > 0) || (value is YamlMappingNode map && map.Children.Count > 0))
            node.Add(key, value);
    }

    private static (Equivalencies client, Equivalencies server, Equivalencies joined) Split(Sided<Equivalencies> sided)
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

    private static List<HashSet<string>> ParseEquivalencies(YamlSequenceNode node)
    {
        var list = new List<HashSet<string>>();
        foreach (var item in node)
        {
            if (item is YamlSequenceNode seq)
            {
                var set = new HashSet<string>(seq.Select(x => x.String()));
                list.Add(set);
            }
            else if (item is YamlMappingNode map)
            {
                foreach (var sub in map.Children)
                {
                    list.Add(new() { sub.Key.String(), sub.Value.String() });
                }
            }
        }
        return list;
    }

    public Equivalencies(params Equivalencies[] merge_in) : this()
    {
        foreach (var item in merge_in)
        {
            foreach (var i in item.Classes)
            {
                AddClasses(i);
            }
            foreach (var i in item.Methods)
            {
                AddMethods(i);
            }
            foreach (var i in item.Fields)
            {
                AddFields(i);
            }
        }
    }

    public Equivalencies()
    {
        EquivalentClasses = new();
        EquivalentFields = new();
        EquivalentMethods = new();
    }

    public Equivalencies(IEnumerable<IEnumerable<string>> classes, IEnumerable<IEnumerable<string>> fields, IEnumerable<IEnumerable<string>> methods)
    {
        EquivalentClasses = classes.Select(x => new HashSet<string>(x)).ToList();
        EquivalentFields = fields.Select(x => new HashSet<string>(x)).ToList();
        EquivalentMethods = methods.Select(x => new HashSet<string>(x)).ToList();
    }

    private IEnumerable<string> GetEquivalencies(List<HashSet<string>> list, string name)
    {
        return (IEnumerable<string>)FindSet(list, name) ?? new[] { name };
    }

    public IEnumerable<string> GetEquivalentClasses(string name)
    {
        return GetEquivalencies(EquivalentClasses, name);
    }

    public IEnumerable<string> GetEquivalentMethods(string name)
    {
        return GetEquivalencies(EquivalentMethods, name);
    }

    public IEnumerable<string> GetEquivalentFields(string name)
    {
        return GetEquivalencies(EquivalentFields, name);
    }

    public void AddClasses(IEnumerable<string> classes)
    {
        AddEquivalencies(EquivalentClasses, classes);
    }

    public void AddMethods(IEnumerable<string> methods)
    {
        AddEquivalencies(EquivalentMethods, methods);
    }

    public void AddFields(IEnumerable<string> fields)
    {
        AddEquivalencies(EquivalentFields, fields);
    }

    private HashSet<string> FindOrCreateSet(List<HashSet<string>> list, string entry)
    {
        var set = FindSet(list, entry);
        if (set != null)
            return set;
        var new_set = new HashSet<string>();
        list.Add(new_set);
        return new_set;
    }

    private HashSet<string> FindSet(List<HashSet<string>> list, string entry)
    {
        foreach (var set in list)
        {
            if (set.Contains(entry))
                return set;
        }
        return null;
    }

    private void AddEquivalencies(List<HashSet<string>> list, IEnumerable<string> items)
    {
        if (!items.Any())
            return;
        var set = FindOrCreateSet(list, items.First());
        foreach (var item in items)
        {
            set.Add(item);
        }
    }
}
