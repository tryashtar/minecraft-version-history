using MinecraftVersionHistory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TryashtarUtils.Utility;

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
        var global_map = new Sided<FlatMap>();
        var mcps = new Dictionary<string, MCP>();
        void add_mcp(MCP mcp)
        {
            if (mcps.TryGetValue(mcp.ClientVersion, out var existing))
            {
                if (MCP.Sorter.Compare(mcp, existing) > 0)
                {
                    mcps[mcp.ClientVersion] = mcp;
                    Console.WriteLine($"Replacing MCP {existing} with {mcp} for version {mcp.ClientVersion}");
                }
                else
                    Console.WriteLine($"Not replacing MCP {existing} with {mcp} for version {mcp.ClientVersion}");
            }
            else
                mcps[mcp.ClientVersion] = mcp;
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

        var jsons = new Dictionary<JObject, string>();
        foreach (var folder in sorted[ArgType.ModernCSV])
        {
            var file = Path.Combine(folder, "versions.json");
            if (File.Exists(file))
            {
                using var reader = File.OpenText(file);
                using var jreader = new JsonTextReader(reader);
                jsons.Add((JObject)JToken.ReadFrom(jreader), folder);
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
                        if (!(json.TryGetValue(series, out var r) && r is JObject results))
                            yield break;
                        if (results.TryGetValue("snapshot", out var n) && n is JArray snapshot)
                        {
                            foreach (var item in snapshot.Select(x => ("snapshot", x.ToString())).Reverse())
                            {
                                yield return item;
                            }
                        }
                        if (results.TryGetValue("stable", out var t) && t is JArray stable)
                        {
                            foreach (var item in stable.Select(x => ("stable", x.ToString())).Reverse())
                            {
                                yield return item;
                            }
                        }
                    }

                    var choices = choose();
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
                        versioned_map.Equivalencies.Client.AddFields(new[] { from, to });
                    else if (from.StartsWith("func_"))
                        versioned_map.Equivalencies.Client.AddMethods(new[] { from, to });
                }
                foreach (var (from, to) in classic.NewIDs.Server)
                {
                    if (from.StartsWith("field_"))
                        versioned_map.Equivalencies.Server.AddFields(new[] { from, to });
                    else if (from.StartsWith("func_"))
                        versioned_map.Equivalencies.Server.AddMethods(new[] { from, to });
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
            versioned_map.WriteTo(Path.Combine(output, "mappings.yaml"));
        }
    }
    class HistoryComparer : IComparer<MCP>
    {
        public int Compare(MCP? x, MCP? y)
        {
            int c = CompareSeries(GetSeries(x), GetSeries(y));
            if (c != 0)
                return c;
            return CompareVersions(x, y);
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
            return x.CompareTo(y);
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
                int c = xs[i].CompareTo(ys[i]);
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