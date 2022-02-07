using Microsoft.VisualBasic.FileIO;

namespace MinecraftVersionHistory;

public class ClassicMCP : MCP
{
    public readonly string ZipPath;
    public readonly int MajorVersion;
    public readonly int MinorVersion;
    public readonly string ExtraVersion;
    public string Version => $"{MajorVersion}.{MinorVersion}{ExtraVersion}";
    public readonly string ClientVersion;
    public readonly string ServerVersion;
    private readonly SidedMappings<TargetedMappings> LocalMappings = new();
    private static readonly Regex MCPVersionRegex = new(@"mcp(?<lead>\d)(?<digits>\d+)(?<extra>.*)");
    private static readonly Regex RevengVersionRegex = new(@"revengpack(?<lead>\d)(?<digits>\d+)(?<extra>.*)");
    private static readonly Regex ClientVersionRegex = new(@"ClientVersion = (?<ver>.*)");
    private static readonly Regex ServerVersionRegex = new(@"ServerVersion = (?<ver>.*)");
    private static readonly Regex ReadMeRegex = new(@"Minecraft mod creator pack (.*?) for Minecraft (?<ver>.*)");
    public ClassicMCP(string path, Dictionary<string, string> version_fallback)
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
                while (!reader.EndOfStream && ClientVersion == null)
                {
                    var line = reader.ReadLine();
                    var m1 = ClientVersionRegex.Match(line);
                    if (m1.Success)
                        ClientVersion = m1.Groups["ver"].Value;
                    var m2 = ServerVersionRegex.Match(line);
                    if (m2.Success)
                        ClientVersion = m2.Groups["ver"].Value;
                }
                if (ClientVersion != null)
                    ClientVersion = ClientVersion.Replace("pre", "-pre");
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
        Console.WriteLine($"Loaded classic MCP {Version} for MC {ClientVersion}");
        LoadMappings(zip);
    }

    public override bool AcceptsVersion(JavaVersion version)
    {
        return ClientVersion == version.Name;
    }

    private record Mapping(string Type, string OldName, string NewName, string Side);

    private void LoadMappings(ZipArchive zip)
    {
        var combined_srg = zip.GetEntry("conf/joined.srg");
        if (combined_srg != null)
        {
            ParseSRG(combined_srg, LocalMappings.Client);
            ParseSRG(combined_srg, LocalMappings.Server);
        }

        var client_srg = zip.GetEntry("conf/client.srg");
        if (client_srg != null)
            ParseSRG(client_srg, LocalMappings.Client);
        var server_srg = zip.GetEntry("conf/server.srg");
        if (server_srg != null)
            ParseSRG(server_srg, LocalMappings.Server);

        var client_rgs = zip.GetEntry("conf/minecraft.rgs") ?? zip.GetEntry(@"conf\minecraft.rgs");
        if (client_rgs != null)
            ParseRGS(client_rgs, LocalMappings.Client);
        var server_rgs = zip.GetEntry("conf/minecraft_server.rgs") ?? zip.GetEntry(@"conf\minecraft_server.rgs");
        if (server_rgs != null)
            ParseRGS(server_rgs, LocalMappings.Server);

        StreamReader read(string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null)
                return null;
            return new(entry.Open());
        }
        ParseCSVs(LocalMappings,
            newids: read("conf/newids.csv"),
            classes: read("conf/classes.csv"),
            methods: read("conf/methods.csv"),
            fields: read("conf/fields.csv")
        );
    }

    public override void CreateClientMappings(string path)
    {
        WriteSRG(path, LocalMappings.Client.Remap(GlobalMappings.Client).Remap(NewIDs.Client).Remap(GlobalMappings.Client));
    }

    public override void CreateServerMappings(string path)
    {
        WriteSRG(path, LocalMappings.Server.Remap(GlobalMappings.Server).Remap(NewIDs.Server).Remap(GlobalMappings.Server));
    }

    private void ParseSRG(ZipArchiveEntry srg, TargetedMappings mappings)
    {
        using var reader = new StreamReader(srg.Open());
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            var entries = line.Split(' ');
            var type = entries[0];
            if (type == "CL:")
                mappings.AddClass(entries[1], entries[2]);
            else if (type == "FD:")
                mappings.AddField(entries[1], entries[2]);
            else if (type == "MD:")
                mappings.AddMethod(entries[1], entries[3], entries[2]);
        }
    }

    private void ParseRGS(ZipArchiveEntry rgs, TargetedMappings mappings)
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
            var name = entries[1];
            // fix for a couple random non-obfuscated classes getting renamed for no reason
            if (name.StartsWith("com/jcraft") || name.StartsWith("paulscode"))
                continue;
            if (type == ".class_map")
                mappings.AddClass(name, entries[2]);
            else if (type == ".field_map")
                mappings.AddField(name, entries[2]);
            else if (type == ".method_map")
                mappings.AddMethod(name, entries[3], entries[2]);
        }
    }
}

