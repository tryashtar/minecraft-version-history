namespace MinecraftVersionHistory;

public class MCP
{
    public readonly string ZipPath;
    public readonly int MajorVersion;
    public readonly int MinorVersion;
    public readonly string ExtraVersion;
    public string Version => $"{MajorVersion}.{MinorVersion}{ExtraVersion}";
    public readonly string ClientVersion;
    public readonly string ServerVersion;
    private readonly List<Mapping> LocalMappings;
    private static readonly Regex MCPVersionRegex = new(@"mcp(?<lead>\d)(?<digits>\d+)(?<extra>.*)");
    private static readonly Regex RevengVersionRegex = new(@"revengpack(?<lead>\d)(?<digits>\d+)(?<extra>.*)");
    private static readonly Regex ClientVersionRegex = new(@"ClientVersion = (?<ver>.*)");
    private static readonly Regex ServerVersionRegex = new(@"ServerVersion = (?<ver>.*)");
    private static readonly Regex ReadMeRegex = new(@"Minecraft mod creator pack (.*?) for Minecraft (?<ver>.*)");
    public static readonly MCPSorter Sorter = new();
    public MCP(string path, Dictionary<string, string> version_fallback)
    {
        ZipPath = path;
        using ZipArchive zip = ZipFile.OpenRead(path);
        var v = MCPVersionRegex.Match(Path.GetFileNameWithoutExtension(path));
        if (!v.Success)
            v = RevengVersionRegex.Match(Path.GetFileNameWithoutExtension(path));
        MajorVersion = int.Parse(v.Groups["lead"].Value);
        MinorVersion = int.Parse(v.Groups["digits"].Value);
        ExtraVersion = v.Groups["extra"].Value;
        if (version_fallback.TryGetValue(Version, out var cv))
        {
            ClientVersion = cv;
            ServerVersion = cv;
        }
        if (ClientVersion == null)
        {
            var vcfg = zip.GetEntry("conf/version.cfg");
            if (vcfg != null)
            {
                using var reader = new StreamReader(vcfg.Open());
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var m1 = ClientVersionRegex.Match(line);
                    if (m1.Success)
                        ClientVersion = m1.Groups["ver"].Value;
                    var m2 = ServerVersionRegex.Match(line);
                    if (m2.Success)
                        ClientVersion = m2.Groups["ver"].Value;
                }
            }
            if (ClientVersion == null)
            {
                var readme =
                    zip.GetEntry("Readme.txt") ??
                    zip.GetEntry("README-MCP.txt") ??
                    zip.GetEntry("README-MCP.TXT") ??
                    zip.GetEntry("docs/README-MCP.TXT");
                if (readme != null)
                {
                    using var reader = new StreamReader(readme.Open());
                    var line = reader.ReadLine();
                    var m = ReadMeRegex.Match(line);
                    if (m.Success)
                    {
                        var ver = m.Groups["ver"].Value;
                        var series = "a";
                        if (MajorVersion > 2 || (MajorVersion == 2 && MinorVersion >= 6))
                            series = "b";
                        if (!ver.StartsWith(series))
                            ver = series + ver;
                        ClientVersion = ver;
                        ServerVersion = ver;
                    }
                }
            }
        }
        if (ClientVersion == null)
            throw new ArgumentException($"Can't figure out what MC version MCP {Version} is for");
        LocalMappings = LoadLocalMappings(zip);
    }

    private record Mapping(string Type, string OldName, string NewName, string Side);

    private List<Mapping> LoadLocalMappings(ZipArchive zip)
    {
        var list = new List<Mapping>();

        var combined_srg = zip.GetEntry("conf/joined.srg");
        if (combined_srg != null)
        {
            list.AddRange(ParseSRG(combined_srg, "0"));
            list.AddRange(ParseSRG(combined_srg, "1"));
            return list;
        }

        var client_srg = zip.GetEntry("conf/client.srg");
        if (client_srg != null)
            list.AddRange(ParseSRG(client_srg, "0"));
        var server_srg = zip.GetEntry("conf/server.srg");
        if (server_srg != null)
            list.AddRange(ParseSRG(server_srg, "1"));
        if (client_srg != null || server_srg != null)
            return list;

        var client_rgs = zip.GetEntry("conf/minecraft.rgs") ?? zip.GetEntry(@"conf\minecraft.rgs");
        if (client_rgs != null)
            list.AddRange(ParseRGS(client_rgs, "0"));
        var server_rgs = zip.GetEntry("conf/minecraft_server.rgs") ?? zip.GetEntry(@"conf\minecraft_server.rgs");
        if (server_rgs != null)
            list.AddRange(ParseRGS(server_rgs, "1"));
        if (client_rgs != null || server_rgs != null)
            return list;

        list.AddRange(ParseCSVs(zip));
        return list;
    }

    public void CreateClientMappings(string path)
    {
        WriteSRG(path, "0");
    }

    public void CreateServerMappings(string path)
    {
        WriteSRG(path, "1");
    }

    private void WriteSRG(string path, string side)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("PK: . net/minecraft/src");
        writer.WriteLine("PK: net net");
        writer.WriteLine("PK: net/minecraft net/minecraft");
        if (side == "0")
        {
            writer.WriteLine("PK: net/minecraft/client net/minecraft/client");
            writer.WriteLine("PK: net/minecraft/client/main net/minecraft/client/main");
            writer.WriteLine("PK: net/minecraft/realms net/minecraft/realms");
            writer.WriteLine("PK: net/minecraft/isom net/minecraft/isom");
        }
        else if (side == "1")
        {
            writer.WriteLine("PK: net/minecraft/server net/minecraft/server");
        }
        foreach (var m in LocalMappings.Where(x => x.Side == side))
        {
            writer.WriteLine($"{m.Type}: {m.OldName} {m.NewName}");
        }
    }

    private IEnumerable<Mapping> ParseSRG(ZipArchiveEntry srg, string side)
    {
        using var reader = new StreamReader(srg.Open());
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            var entries = line.Split(' ');
            var type = entries[0][..^1];
            if (entries.Length == 3 || entries.Length == 4)
                yield return new Mapping(type, entries[1], entries[2], side);
            else if (entries.Length == 5 || entries.Length == 6)
                yield return new Mapping(type, $"{entries[1]} {entries[2]}", $"{entries[3]} {entries[4]}", side);
            else
                throw new InvalidOperationException($"Can't parse SRG line {line}");
        }
    }

    private IEnumerable<Mapping> ParseRGS(ZipArchiveEntry rgs, string side)
    {
        using var reader = new StreamReader(rgs.Open());
        var class_dict = new Dictionary<string, string>();
        var field_set = new HashSet<string>();
        var method_set = new HashSet<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            var entries = line.Split(' ');
            var type = entries[0];
            var name = entries[1].TrimEnd('_');
            if (type == ".class_map" && !name.Contains('/') && !class_dict.ContainsKey(name))
            {
                class_dict[name] = entries[2];
                yield return new Mapping("CL", name, entries[2], side);
            }
            else if (type == ".field_map" || type == ".method_map")
            {
                var classpath = name[..name.LastIndexOf('/')];
                var newpath = class_dict.GetValueOrDefault(classpath, classpath);
                if (type == ".field_map" && !field_set.Contains(name))
                {
                    field_set.Add(name);
                    yield return new Mapping("FD", name, $"{newpath}/{entries[2]}", side);
                }
                else if (type == ".method_map" && !method_set.Contains(name))
                {
                    method_set.Add(name);
                    var newsig = entries[2];
                    foreach (var (key, value) in class_dict)
                    {
                        newsig = newsig.Replace($"L{key};", $"L{value};");
                    }
                    yield return new Mapping("MD", $"{name} {entries[2]}", $"{newpath}/{entries[3]} {newsig}", side);
                }
            }
        }
    }

    private IEnumerable<Mapping> ParseCSVs(ZipArchive zip)
    {
        static bool filter(Dictionary<string, string> x) => x["package"] != "";
        var classes = ParseCSV(zip.GetEntry("conf/classes.csv")).Where(filter);
        var methods = ParseCSV(zip.GetEntry("conf/methods.csv")).Where(filter);
        var fields = ParseCSV(zip.GetEntry("conf/fields.csv")).Where(filter);
        foreach (var c in classes)
        {
            yield return new Mapping("CL", c["notch"], $"{c["package"]}/{c["name"]}", c["side"]);
        }
        foreach (var c in fields)
        {
            yield return new Mapping("FD", $"{c["classnotch"]}/{c["notch"]}", $"{c["package"]}/{c["classname"]}/{c["searge"]}", c["side"]);
        }
        foreach (var c in methods)
        {
            yield return new Mapping("MD", $"{c["classnotch"]}/{c["notch"]} {c["notchsig"]}", $"{c["package"]}/{c["classname"]}/{c["searge"]} {c["sig"]}", c["side"]);
        }
    }

    private List<Dictionary<string, string>> ParseCSV(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var result = new List<Dictionary<string, string>>();
        bool first = true;
        string[] keys = null;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var items = line.Split(',').Select(ParseQuotedString).ToArray();
            if (first)
            {
                first = false;
                keys = items;
            }
            else
            {
                var dict = new Dictionary<string, string>();
                for (int i = 0; i < items.Length; i++)
                {
                    dict[keys[i]] = items[i];
                }
                result.Add(dict);
            }
        }
        return result;
    }

    private string ParseQuotedString(string quoted)
    {
        return quoted.Replace("\"", "");
    }
}

public class MCPSorter : IComparer<MCP>
{
    public int Compare(MCP x, MCP y)
    {
        int m = x.MajorVersion.CompareTo(y.MajorVersion);
        if (m != 0)
            return m;
        int m2 = x.MinorVersion.CompareTo(y.MinorVersion);
        if (m2 != 0)
            return m2;
        return x.ExtraVersion.CompareTo(y.ExtraVersion);
    }
}
