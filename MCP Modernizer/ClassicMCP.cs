using Microsoft.VisualBasic.FileIO;
using MinecraftVersionHistory;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace MCPModernizer;

public class ClassicMCP : MCP
{
    public readonly string ZipPath;
    public readonly int MajorVersion;
    public readonly int MinorVersion;
    public readonly string ExtraVersion;
    public readonly Sided<Dictionary<string, string>> NewIDs = new();
    public string Version => $"{MajorVersion}.{MinorVersion}{ExtraVersion}";
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
            ClientVersion = cv;
        if (ClientVersion == null)
        {
            var vcfg = zip.GetEntry("conf/version.cfg");
            if (vcfg != null)
            {
                using var reader = new StreamReader(vcfg.Open());
                while (!reader.EndOfStream && ClientVersion == null)
                {
                    var line = reader.ReadLine()!;
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
                    var line = reader.ReadLine()!;
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
                    }
                }
            }
        }
        if (ClientVersion == null)
            throw new ArgumentException($"Can't figure out what MC version MCP {Version} is for");

        StreamReader? read(string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null)
                return null;
            return new(entry.Open());
        }
        using (var joined_srg = read("conf/joined.srg"))
        {
            if (joined_srg != null)
                MappingsIO.ParseSrg(LocalMappings.Client, joined_srg);
        }
        using (var joined_srg = read("conf/joined.srg"))
        {
            if (joined_srg != null)
                MappingsIO.ParseSrg(LocalMappings.Client, joined_srg);
        }

        using var client_srg = read("conf/client.srg");
        if (client_srg != null)
            MappingsIO.ParseSrg(LocalMappings.Client, client_srg);
        using var server_srg = read("conf/server.srg");
        if (server_srg != null)
            MappingsIO.ParseSrg(LocalMappings.Server, server_srg);

        using var client_rgs = read("conf/minecraft.rgs") ?? read(@"conf\minecraft.rgs");
        if (client_rgs != null)
            ParseRGS(LocalMappings.Client, client_rgs);
        using var server_rgs = read("conf/minecraft_server.rgs") ?? read(@"conf\minecraft_server.rgs");
        if (server_rgs != null)
            ParseRGS(LocalMappings.Server, server_rgs);

        var newids = read("conf/newids.csv");
        if (newids != null)
            ParseNewIDs(newids);

        ParseCSVs(
            classes: read("conf/classes.csv"),
            methods: read("conf/methods.csv"),
            fields: read("conf/fields.csv")
        );
    }

    private void ParseNewIDs(StreamReader reader)
    {
        var ids = ParseCSV(reader).ToList();
        foreach (var item in ids.Skip(1))
        {
            if (item[0] != "*")
                NewIDs.Client.Add(item[0], item[2]);
            if (item[1] != "*")
                NewIDs.Server.Add(item[1], item[2]);
        }
    }

    private void ParseRGS(Mappings mappings, StreamReader reader)
    {
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
                mappings.AddClass(name.Replace('/', '.'), entries[2].Replace('/', '.'));
            if (type == ".class")
                mappings.AddClass(name.Replace('/', '.'), name.Replace('/', '.'));
            else if (type == ".field_map")
            {
                var (path, namepart) = MappingsIO.Split(name);
                mappings.GetOrAddClass(path).AddField(namepart, entries[2]);
            }
            else if (type == ".method_map")
            {
                var (path, namepart) = MappingsIO.Split(name);
                mappings.GetOrAddClass(path).AddMethod(namepart, entries[3], entries[2]);
            }
        }
    }
}

