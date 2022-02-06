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
    private static readonly Regex MCPVersionRegex = new(@"mcp(?<lead>\d)(?<digits>\d+)(?<extra>.*)");
    private static readonly Regex RevengVersionRegex = new(@"revengpack(?<lead>\d)(?<digits>\d+)(?<extra>.*)");
    private static readonly Regex ClientVersionRegex = new(@"ClientVersion = (?<ver>.*)");
    private static readonly Regex ServerVersionRegex = new(@"ServerVersion = (?<ver>.*)");
    private static readonly Regex ReadMeRegex = new(@"Minecraft mod creator pack (.*?) for Minecraft (?<ver>.*)");
    public static readonly MCPSorter Sorter = new();
    public MCP(string path, Dictionary<string, string> version_fallback)
    {
        ZipPath = path;
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
            using ZipArchive zip = ZipFile.OpenRead(path);
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
    }

    private record Mapping(string Type, string OldName, string NewName);

    private void CreateMappings(string path, ZipArchive zip, string side)
    {
        bool filter(Dictionary<string, string> x) => x["side"] == side && x["package"] != "";
        var classes = ParseCSV(zip.GetEntry("conf/classes.csv")).Where(filter);
        var methods = ParseCSV(zip.GetEntry("conf/methods.csv")).Where(filter);
        var fields = ParseCSV(zip.GetEntry("conf/fields.csv")).Where(filter);
        var mappings = new List<Mapping>();
        foreach (var c in classes)
        {
            mappings.Add(new Mapping("CL", c["notch"], $"{c["package"]}/{c["name"]}"));
        }
        foreach (var c in fields)
        {
            mappings.Add(new Mapping("FD", $"{c["classnotch"]}/{c["notch"]}", $"{c["package"]}/{c["classname"]}/{c["searge"]}"));
        }
        foreach (var c in methods)
        {
            mappings.Add(new Mapping("MD", $"{c["classnotch"]}/{c["notch"]} {c["notchsig"]}", $"{c["package"]}/{c["classname"]}/{c["searge"]} {c["sig"]}"));
        }
        WriteMappings(path, mappings, side);
    }

    private void WriteMappings(string path, IEnumerable<Mapping> mappings, string side)
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
        foreach (var m in mappings)
        {
            writer.WriteLine($"{m.Type}: {m.OldName} {m.NewName}");
        }
    }

    private void RewriteRGS(string path, ZipArchiveEntry rgs, string side)
    {
        using var reader = new StreamReader(rgs.Open());
        var mappings = new List<Mapping>();
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
                mappings.Add(new Mapping("CL", name, entries[2]));
            }
            else if (type == ".field_map" || type == ".method_map")
            {
                var classpath = name[..name.LastIndexOf('/')];
                var newpath = class_dict.GetValueOrDefault(classpath, classpath);
                if (type == ".field_map" && !field_set.Contains(name))
                {
                    field_set.Add(name);
                    mappings.Add(new Mapping("FD", name, $"{newpath}/{entries[2]}"));
                }
                else if (type == ".method_map" && !method_set.Contains(name))
                {
                    method_set.Add(name);
                    var newsig = entries[2];
                    foreach (var (key, value) in class_dict)
                    {
                        newsig = newsig.Replace($"L{key};", $"L{value};");
                    }
                    mappings.Add(new Mapping("MD", $"{name} {entries[2]}", $"{newpath}/{entries[3]} {newsig}"));
                }
            }
        }
        WriteMappings(path, mappings, side);
    }

    public void CreateClientMappings(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(ZipPath);
        var client =
            zip.GetEntry("conf/client.srg") ??
            zip.GetEntry("conf/joined.srg");
        if (client != null)
            client.ExtractToFile(path, true);
        else
        {
            var rgs = zip.GetEntry("conf/minecraft.rgs") ??
                zip.GetEntry(@"conf\minecraft.rgs");
            if (rgs != null)
                RewriteRGS(path, rgs, "0");
            else
                CreateMappings(path, zip, "0");
        }
    }

    public void CreateServerMappings(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(ZipPath);
        var server =
            zip.GetEntry("conf/server.srg") ??
            zip.GetEntry("conf/joined.srg");
        if (server != null)
            server.ExtractToFile(path, true);
        else
        {
            var rgs = zip.GetEntry("conf/minecraft_server.rgs") ??
                zip.GetEntry(@"conf\minecraft_server.rgs") ??
                zip.GetEntry("conf/minecraft.rgs") ??
                zip.GetEntry(@"conf\minecraft.rgs");
            if (rgs != null)
                RewriteRGS(path, rgs, "1");
            else
                CreateMappings(path, zip, "1");
        }
    }

    private string ParseQuotedString(string quoted)
    {
        return quoted.Replace("\"", "");
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