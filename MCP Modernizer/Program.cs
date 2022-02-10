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
        void find_version(string parent, string type, string version, string series)
        {
            string tsrg = Path.Combine(parent, "joined.tsrg");
            if (File.Exists(tsrg))
            {
                foreach (var (json, folder) in jsons)
                {
                    (string type, string number)? choose(string? prefer, string? secondary)
                    {
                        if (prefer == null)
                            return null;
                        if (!(json.TryGetValue(series, out var r) && r is JObject results))
                            return null;
                        if (!(results.TryGetValue(prefer, out var l) && l is JArray list))
                            return null;
                        if (list.Count == 0)
                            return choose(secondary, null);
                        return (prefer, list[0].ToString());
                    }

                    var choice = choose(type, type == "stable" ? "snapshot" : "stable");
                    if (choice != null)
                    {
                        var csvs = Path.Combine(folder, $"mcp_{choice.Value.type}", $"{choice.Value.number}-{series}", $"mcp_{choice.Value.type}-{choice.Value.number}-{series}.zip");
                        var mcp = new ModernMCP(version, tsrg, csvs);
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
                        find_version(sub, "stable", Path.GetFileName(sub), Path.GetFileName(sub));
                    else
                    {
                        foreach (var deeper in Directory.GetDirectories(sub))
                        {
                            if (type == "snapshot")
                                find_version(deeper, "snapshot", Path.GetFileName(deeper), Path.GetFileName(sub));
                            else if (type == "pre")
                            {
                                string version = Path.GetFileName(deeper);
                                int dash = version.IndexOf('-');
                                string series = version[..dash];
                                find_version(deeper, "stable", version, series);
                            }
                        }
                    }
                }
            }
        }
        foreach (var output in sorted[ArgType.Output])
        {
            foreach (var mcp in mcps.Values)
            {
                string dir = Path.Combine(output, mcp.ClientVersion);
                Directory.CreateDirectory(dir);
                mcp.WriteClientMappings(Path.Combine(dir, "client.tsrg"));
                mcp.WriteServerMappings(Path.Combine(dir, "server.tsrg"));
                mcp.WriteClientFriendlies(
                    Path.Combine(dir, "client_fields.csv"),
                    Path.Combine(dir, "client_methods.csv")
                );
                mcp.WriteServerFriendlies(
                    Path.Combine(dir, "server_fields.csv"),
                    Path.Combine(dir, "server_methods.csv")
                );
            }
        }
    }
}