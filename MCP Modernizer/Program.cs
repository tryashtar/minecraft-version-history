using System.Text.Json;
using System.Text.Json.Nodes;
using MinecraftVersionHistory;
using TryashtarUtils.Utility;
using YamlDotNet.RepresentationModel;

namespace MCPModernizer;

enum ArgType
{
    Classic,
    ModernSRG,
    ModernCSV,
    Output
}
// the most schizophrenic code you've ever seen in your life
public static class Program
{
    public static void Main(string[] args)
    {
        ArgType? parsing = null;
        var sorted = new Dictionary<ArgType, List<string>>
        {
            [ArgType.Classic] = new(),
            [ArgType.ModernSRG] = new(),
            [ArgType.ModernCSV] = new(),
            [ArgType.Output] = new()
        };
        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                parsing = arg switch
                {
                    "--classic" => ArgType.Classic,
                    "--modern-srg" => ArgType.ModernSRG,
                    "--modern-csv" => ArgType.ModernCSV,
                    "--output" => ArgType.Output,
                    _ => throw new ArgumentException()
                };
            }
            else if (parsing != null)
                sorted[parsing.Value].Add(arg);
        }
        var versioned_map = new VersionedRenames();
        var mcps = new Dictionary<string, MCP>();
        void add_mcp(MCP mcp)
        {
            if (mcps.TryGetValue(mcp.ClientVersion, out var existing))
            {
                if (MCP.Sorter.Compare(mcp, existing) > 0)
                    Merge2MCPs(mcps[mcp.ClientVersion], mcp);
                else
                    MergeMCPs(mcps[mcp.ClientVersion], mcp, mcps[mcp.ClientVersion]);
            }
            else
                mcps[mcp.ClientVersion] = mcp;
        }
        static void MergeMCPs(MCP destination, MCP mcp1, MCP mcp2)
        {
            Merge2MCPs(destination, mcp1);
            Merge2MCPs(destination, mcp2);
        }
        static void Merge2MCPs(MCP destination, MCP mcp1)
        {
            Console.WriteLine($"Merging {destination.ClientVersion} and {mcp1.ClientVersion}");
            foreach (var item in mcp1.LocalMappings.Client.ClassList.ToList())
            {
                var cl = destination.LocalMappings.Client.AddClass(item.OldName, item.NewName);
                foreach (var i in item.FieldList.ToList())
                {
                    cl.AddField(i.OldName, i.NewName);
                }
                foreach (var i in item.MethodList.ToList())
                {
                    cl.AddMethod(i.OldName, i.NewName, i.Signature);
                }
            }
            foreach (var item in mcp1.LocalMappings.Server.ClassList.ToList())
            {
                var cl = destination.LocalMappings.Server.AddClass(item.OldName, item.NewName);
                foreach (var i in item.FieldList.ToList())
                {
                    cl.AddField(i.OldName, i.NewName);
                }
                foreach (var i in item.MethodList.ToList())
                {
                    cl.AddMethod(i.OldName, i.NewName, i.Signature);
                }
            }
            foreach (var item in mcp1.FriendlyNames.Client.ClassMap.ToList())
            {
                destination.FriendlyNames.Client.AddClass(item.OldName, item.NewName);
            }
            foreach (var item in mcp1.FriendlyNames.Client.MethodMap.ToList())
            {
                destination.FriendlyNames.Client.AddMethod(item.OldName, item.NewName);
            }
            foreach (var item in mcp1.FriendlyNames.Client.FieldMap.ToList())
            {
                destination.FriendlyNames.Client.AddField(item.OldName, item.NewName);
            }
            foreach (var item in mcp1.FriendlyNames.Server.ClassMap.ToList())
            {
                destination.FriendlyNames.Server.AddClass(item.OldName, item.NewName);
            }
            foreach (var item in mcp1.FriendlyNames.Server.MethodMap.ToList())
            {
                destination.FriendlyNames.Server.AddMethod(item.OldName, item.NewName);
            }
            foreach (var item in mcp1.FriendlyNames.Server.FieldMap.ToList())
            {
                destination.FriendlyNames.Server.AddField(item.OldName, item.NewName);
            }
            Console.WriteLine("Merge done");
        }
        foreach (var folder in sorted[ArgType.Classic])
        {
            var fallback = new Dictionary<string, string>();
            var version_map_file = Path.Combine(folder, "versions.yaml");
            if (File.Exists(version_map_file))
                fallback = YamlHelper.ParseFile(version_map_file).ToDictionary();
            foreach (var file in Directory.GetFiles(folder, "*.zip"))
            {
                var mcp = new ClassicMCP(file, fallback);
                Console.WriteLine($"Classic MCP for {mcp.ClientVersion}");
                add_mcp(mcp);
            }
        }

        var jsons = new Dictionary<JsonObject, string>();
        foreach (var folder in sorted[ArgType.ModernCSV])
        {
            var file = Path.Combine(folder, "versions.json");
            if (File.Exists(file))
            {
                using var reader = File.OpenRead(file);
                jsons.Add(JsonSerializer.Deserialize<JsonObject>(reader)!, folder);
            }
        }
        void find_version(string parent, string version, string series)
        {
            string tsrg = Path.Combine(parent, "joined.tsrg");
            if (File.Exists(tsrg))
            {
                foreach (var (json, folder) in jsons)
                {
                    IEnumerable<(string type, string number)> choose()
                    {
                        if (!(json.TryGetPropertyValue(series, out var r) && r is JsonObject results))
                            yield break;
                        if (results.TryGetPropertyValue("snapshot", out var n) && n is JsonArray snapshot)
                        {
                            foreach (var item in snapshot.Select(x => ("snapshot", x!.ToString())).Reverse())
                            {
                                yield return item;
                            }
                        }
                        if (results.TryGetPropertyValue("stable", out var t) && t is JsonArray stable)
                        {
                            foreach (var item in stable.Select(x => ("stable", x!.ToString())).Reverse())
                            {
                                yield return item;
                            }
                        }
                    }

                    var choices = choose().ToList();
                    if (choices.Any())
                    {
                        var csvs = choices.Select(x => Path.Combine(folder, $"mcp_{x.type}", $"{x.number}-{series}", $"mcp_{x.type}-{x.number}-{series}.zip"));
                        var mcp = new ModernMCP(version, series, tsrg, csvs.ToArray());
                        Console.WriteLine($"Modern MCP for {mcp.ClientVersion}");
                        add_mcp(mcp);
                    }
                    else
                        Console.WriteLine($"Couldn't find CSVs for {version} in {series}");
                }
            }
        }
        foreach (var folder in sorted[ArgType.ModernSRG])
        {
            string[] types = { "pre", "release", "snapshot" };
            foreach (var type in types)
            {
                foreach (var sub in Directory.GetDirectories(Path.Combine(folder, type)))
                {
                    if (type == "release")
                        find_version(sub, Path.GetFileName(sub), Path.GetFileName(sub));
                    else
                    {
                        foreach (var deeper in Directory.GetDirectories(sub))
                        {
                            if (type == "snapshot")
                                find_version(deeper, Path.GetFileName(deeper), Path.GetFileName(sub));
                            else if (type == "pre")
                            {
                                string version = Path.GetFileName(deeper);
                                int dash = version.IndexOf('-');
                                string series = version[..dash];
                                find_version(deeper, version, series);
                            }
                        }
                    }
                }
            }
        }

        var class_renames = new Dictionary<(Rename rename, string side), HashSet<string>>();
        var field_renames = new Dictionary<(Rename rename, string side), HashSet<string>>();
        var method_renames = new Dictionary<(Rename rename, string side), HashSet<string>>();
        var latest = new Sided<FlatMap>();
        var equivs = new Sided<Equivalencies>();
        var not_found_report = new Dictionary<string, Dictionary<string, string>>();
        static void add_to(Dictionary<(Rename, string), HashSet<string>> dict, IEnumerable<Rename> stuff, string side, string version)
        {
            foreach (var item in stuff)
            {
                if (!dict.ContainsKey((item, side)))
                    dict[(item, side)] = new();
                dict[(item, side)].Add(version);
            }
        }

        foreach (var mcp in mcps.Values.OrderBy(x => x, new HistoryComparer()))
        {
            Console.WriteLine(mcp.ClientVersion);
            add_to(class_renames, mcp.FriendlyNames.Client.ClassMap, "client", mcp.ClientVersion);
            add_to(class_renames, mcp.FriendlyNames.Server.ClassMap, "server", mcp.ClientVersion);
            add_to(field_renames, mcp.FriendlyNames.Client.FieldMap, "client", mcp.ClientVersion);
            add_to(field_renames, mcp.FriendlyNames.Server.FieldMap, "server", mcp.ClientVersion);
            add_to(method_renames, mcp.FriendlyNames.Client.MethodMap, "client", mcp.ClientVersion);
            add_to(method_renames, mcp.FriendlyNames.Server.MethodMap, "server", mcp.ClientVersion);
            foreach (var item in mcp.FriendlyNames.Client.ClassMap)
            {
                latest.Client.AddClass(item.OldName, item.NewName);
            }
            foreach (var item in mcp.FriendlyNames.Client.FieldMap)
            {
                latest.Client.AddField(item.OldName, item.NewName);
            }
            foreach (var item in mcp.FriendlyNames.Client.MethodMap)
            {
                latest.Client.AddMethod(item.OldName, item.NewName);
            }
            foreach (var item in mcp.FriendlyNames.Server.ClassMap)
            {
                latest.Server.AddClass(item.OldName, item.NewName);
            }
            foreach (var item in mcp.FriendlyNames.Server.FieldMap)
            {
                latest.Server.AddField(item.OldName, item.NewName);
            }
            foreach (var item in mcp.FriendlyNames.Server.MethodMap)
            {
                latest.Server.AddMethod(item.OldName, item.NewName);
            }
            if (mcp is ClassicMCP classic)
            {
                foreach (var (from, to) in classic.NewIDs.Client)
                {
                    if (from.StartsWith("field_"))
                        equivs.Client.AddFields(new[] { from, to });
                    else if (from.StartsWith("func_"))
                        equivs.Client.AddMethods(new[] { from, to });
                }
                foreach (var (from, to) in classic.NewIDs.Server)
                {
                    if (from.StartsWith("field_"))
                        equivs.Server.AddFields(new[] { from, to });
                    else if (from.StartsWith("func_"))
                        equivs.Server.AddMethods(new[] { from, to });
                }
            }
            foreach (var output in sorted[ArgType.Output])
            {
                string dir = Path.Combine(output, mcp.ClientVersion);
                Directory.CreateDirectory(dir);
                mcp.WriteClientMappings(Path.Combine(dir, "client.tsrg"));
                mcp.WriteServerMappings(Path.Combine(dir, "server.tsrg"));
            }
        }
        var reversed_dict = new Dictionary<HashSet<string>, Sided<FlatMap>>(HashSet<string>.CreateSetComparer());
        void apply(Dictionary<(Rename rename, string side), HashSet<string>> dict, Action<FlatMap, string, string> adder)
        {
            foreach (var pair in dict)
            {
                if (!reversed_dict.ContainsKey(pair.Value))
                    reversed_dict[pair.Value] = new();
                if (pair.Key.side == "client")
                    adder(reversed_dict[pair.Value].Client, pair.Key.rename.OldName, pair.Key.rename.NewName);
                else if (pair.Key.side == "server")
                    adder(reversed_dict[pair.Value].Server, pair.Key.rename.OldName, pair.Key.rename.NewName);
            }
        }
        apply(class_renames, (x, y, z) => x.AddClass(y, z));
        apply(field_renames, (x, y, z) => x.AddField(y, z));
        apply(method_renames, (x, y, z) => x.AddMethod(y, z));
        foreach (var (versions, map) in reversed_dict)
        {
            versioned_map.Add(new(versions), map);
        }

        versioned_map.Add(VersionSpec.All, latest);
        foreach (var output in sorted[ArgType.Output])
        {
            versioned_map.WriteTo(Path.Combine(output, "mappings_found.yaml"));
            Equivalencies.WriteTo(equivs, Path.Combine(output, "equivalencies_found.yaml"));
        }

        foreach (var mcp in mcps.Values)
        {
            Console.WriteLine(mcp.ClientVersion);
            foreach (var c in mcp.LocalMappings.Client.ClassList)
            {
                var end_name = c.NewName.Split('.')[^1];
                if (end_name.StartsWith("C_"))
                {
                    if (!not_found_report.ContainsKey(c.NewName))
                        not_found_report[c.NewName] = new();
                    not_found_report[c.NewName][mcp.ClientVersion] = c.NewName;
                }
                foreach (var m in c.MethodList)
                {
                    if (!m.NewName.StartsWith("func_"))
                        continue;
                    var match = versioned_map.GetClientMethod(mcp.ClientVersion, m.NewName, equivs.Client);
                    if (match == null)
                    {
                        if (!not_found_report.ContainsKey(m.NewName))
                            not_found_report[m.NewName] = new();
                        not_found_report[m.NewName][mcp.ClientVersion] = c.NewName;
                    }
                }
            }
            foreach (var c in mcp.LocalMappings.Server.ClassList)
            {
                var end_name = c.NewName.Split('.')[^1];
                if (end_name.StartsWith("C_"))
                {
                    if (!not_found_report.ContainsKey(c.NewName))
                        not_found_report[c.NewName] = new();
                    not_found_report[c.NewName][mcp.ClientVersion] = c.NewName;
                }
                foreach (var m in c.MethodList)
                {
                    if (!m.NewName.StartsWith("func_"))
                        continue;
                    var match = versioned_map.GetServerMethod(mcp.ClientVersion, m.NewName, equivs.Server);
                    if (match == null)
                    {
                        if (!not_found_report.ContainsKey(m.NewName))
                            not_found_report[m.NewName] = new();
                        not_found_report[m.NewName][mcp.ClientVersion] = c.NewName;
                    }
                }
            }
            foreach (var c in mcp.LocalMappings.Client.ClassList)
            {
                foreach (var m in c.FieldList)
                {
                    if (!m.NewName.StartsWith("field_"))
                        continue;
                    var match = versioned_map.GetClientField(mcp.ClientVersion, m.NewName, equivs.Client);
                    if (match == null)
                    {
                        if (!not_found_report.ContainsKey(m.NewName))
                            not_found_report[m.NewName] = new();
                        not_found_report[m.NewName][mcp.ClientVersion] = c.NewName;
                    }
                }
            }
            foreach (var c in mcp.LocalMappings.Server.ClassList)
            {
                foreach (var m in c.FieldList)
                {
                    if (!m.NewName.StartsWith("field_"))
                        continue;
                    var match = versioned_map.GetServerField(mcp.ClientVersion, m.NewName, equivs.Server);
                    if (match == null)
                    {
                        if (!not_found_report.ContainsKey(m.NewName))
                            not_found_report[m.NewName] = new();
                        not_found_report[m.NewName][mcp.ClientVersion] = c.NewName;
                    }
                }
            }
        }
        var nfp = new YamlMappingNode();
        foreach (var pair in not_found_report.OrderBy(x => x.Value.Count).ThenBy(x => x.Key))
        {
            var v = new YamlMappingNode();
            foreach (var pair2 in pair.Value)
            {
                v.Add(pair2.Key, pair2.Value);
            }
            if (!pair.Value.ContainsKey("1.14.4"))
                nfp.Add(pair.Key, v);
        }
        foreach (var output in sorted[ArgType.Output])
        {
            YamlHelper.SaveToFile(nfp, Path.Combine(output, "missing.yaml"));
        }
    }
    class HistoryComparer : IComparer<MCP>
    {
        public int Compare(MCP? x, MCP? y)
        {
            int c = CompareSeries(GetSeries(x!), GetSeries(y!));
            if (c != 0)
                return c;
            return CompareVersions(x!, y!);
        }

        private int CompareVersions(MCP x, MCP y)
        {
            if (x.ClientVersion.Contains("w") && y.ClientVersion.Contains("w"))
                return CompareSnapshots(x.ClientVersion, y.ClientVersion);
            if (x.ClientVersion.Contains("w"))
                return -1;
            if (y.ClientVersion.Contains("w"))
                return 1;
            return CompareSeries(x.ClientVersion, y.ClientVersion);
        }

        private int CompareSnapshots(string x, string y)
        {
            return String.Compare(x, y, StringComparison.Ordinal);
        }

        private int CompareSeries(string x, string y)
        {
            string[] xs = x.Split('.');
            string[] ys = y.Split('.');
            if (x.StartsWith("a"))
            {
                if (y.StartsWith("b") || !y.StartsWith("a"))
                    return -1;
            }
            if (y.StartsWith("a"))
            {
                if (x.StartsWith("b") || !x.StartsWith("a"))
                    return 1;
            }
            if (x.StartsWith("b"))
            {
                if (y.StartsWith("a"))
                    return 1;
                if (!y.StartsWith("b"))
                    return -1;
            }
            if (y.StartsWith("b"))
            {
                if (x.StartsWith("a"))
                    return -1;
                if (!x.StartsWith("b"))
                    return 1;
            }
            for (int i = 0; i < Math.Min(xs.Length, ys.Length); i++)
            {

                if (xs[i].Contains("-pre") && ys[i].Contains("-pre"))
                {
                    xs[i] = xs[i].Replace("-pre", "");
                    ys[i] = ys[i].Replace("-pre", "");
                }
                if (int.TryParse(xs[i], out int xsi) && int.TryParse(ys[i], out int ysi))
                {
                    int ic = xsi.CompareTo(ysi);
                    if (ic != 0)
                        return ic;
                }
                int c = String.Compare(xs[i], ys[i], StringComparison.Ordinal);
                if (c != 0)
                    return c;
            }
            int c2 = xs.Length.CompareTo(ys.Length);
            return c2;
        }

        private string GetSeries(MCP m)
        {
            if (m.ClientVersion.StartsWith("12w"))
                return "1.3";
            if (m.ClientVersion.StartsWith("13w"))
                return "1.5";
            if (m is ClassicMCP c)
                return c.ClientVersion;
            else if (m is ModernMCP a)
                return a.Series;
            else throw new Exception();
        }
    }
}