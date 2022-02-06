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

    private void CreateMappings(string path, ZipArchive zip, string side)
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
        bool filter(Dictionary<string, string> x) => x["side"] == side && x["package"] != "";
        var classes = ParseCSV(zip.GetEntry("conf/classes.csv")).Where(filter);
        var methods = ParseCSV(zip.GetEntry("conf/methods.csv")).Where(filter);
        var fields = ParseCSV(zip.GetEntry("conf/fields.csv")).Where(filter);
        foreach (var c in classes)
        {
            writer.WriteLine($"CL: {c["notch"]} {c["package"]}/{c["name"]}");
        }
        foreach (var c in fields)
        {
            writer.WriteLine($"FD: {c["classnotch"]}/{c["notch"]} {c["package"]}/{c["classname"]}/{c["searge"]}");
        }
        foreach (var c in methods)
        {
            writer.WriteLine($"MD: {c["classnotch"]}/{c["notch"]} {c["notchsig"]} {c["package"]}/{c["classname"]}/{c["searge"]} {c["sig"]}");
        }
    }

    public void CreateClientMappings(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(ZipPath);
        var client =
            zip.GetEntry("conf/client.srg") ??
            zip.GetEntry("conf/minecraft.rgs") ??
            zip.GetEntry(@"conf\minecraft.rgs") ??
            zip.GetEntry("conf/joined.srg");
        if (client != null)
            client.ExtractToFile(path, true);
        else
            CreateMappings(path, zip, "0");
    }

    public void CreateServerMappings(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(ZipPath);
        var server =
            zip.GetEntry("conf/server.srg") ??
            zip.GetEntry("conf/minecraft_server.rgs") ??
            zip.GetEntry(@"conf\minecraft_server.rgs") ??
            zip.GetEntry("conf/joined.srg") ??
            zip.GetEntry("conf/minecraft.rgs") ??
            zip.GetEntry(@"conf\minecraft.rgs");
        if (server != null)
            server.ExtractToFile(path, true);
        else
            CreateMappings(path, zip, "1");
    }

    private string ParseQuotedString(string quoted)
    {
        return quoted.Replace("\"", "");
    }

    private List<Dictionary<string, string>> ParseCSV(ZipArchiveEntry entry)
    {
        using (var a = new StreamReader(entry.Open()))
        {
            File.WriteAllText(entry.Name.Replace("/", ""), a.ReadToEnd());
        }
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