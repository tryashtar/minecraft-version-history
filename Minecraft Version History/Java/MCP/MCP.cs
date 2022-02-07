using Microsoft.VisualBasic.FileIO;

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
    private readonly SidedMappings<TargetedMappings> LocalMappings = new();
    private static readonly SidedMappings<UntargetedMappings> GlobalMappings = new();
    private static readonly SidedMappings<UntargetedMappings> NewIDs = new();
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
        LoadMappings(zip);
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

        ParseCSVs(zip);
    }

    public void CreateClientMappings(string path)
    {
        WriteSRG(path, LocalMappings.Client.Remap(GlobalMappings.Client).Remap(NewIDs.Client).Remap(GlobalMappings.Client));
    }

    public void CreateServerMappings(string path)
    {
        WriteSRG(path, LocalMappings.Server.Remap(GlobalMappings.Server).Remap(NewIDs.Server).Remap(GlobalMappings.Server));
    }

    private void WriteSRG(string path, TargetedMappings mappings)
    {
        using var writer = new StreamWriter(path);
        // export as TSRG format, which resembles proguard more than SRG
        // each class lists its properties in turn instead of duplicating the class name for each
        // also we don't need the deobfuscated method signature
        foreach (var c in mappings.Classes.Values)
        {
            writer.WriteLine($"{TargetedMappings.Split(c.OldName).name} {c.NewName}");
            foreach (var f in c.Fields)
            {
                writer.WriteLine($"\t{f.Key} {f.Value}");
            }
            foreach (var m in c.Methods.Values)
            {
                writer.WriteLine($"\t{m.OldName} {m.Signature} {m.NewName}");
            }
        }
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

    private void ParseCSVs(ZipArchive zip)
    {
        var id_csv = zip.GetEntry("conf/newids.csv");
        if (id_csv != null)
        {
            var ids = ParseCSV(id_csv).ToList();
            static void add(UntargetedMappings mappings, string from, string to)
            {
                if (from.StartsWith("field_"))
                    mappings.AddField(from, to);
                else if (from.StartsWith("func_"))
                    mappings.AddMethod(from, to);
            }
            foreach (var item in ids.Skip(1))
            {
                if (item[0] != "*")
                    add(NewIDs.Client, item[0], item[2]);
                if (item[1] != "*")
                    add(NewIDs.Server, item[1], item[2]);
            }
        }
        var class_csv = zip.GetEntry("conf/classes.csv");
        if (class_csv != null)
        {
            var classes = ParseCSV(class_csv).ToList();
            if (classes[0][0] == "name")
            {
                // 3.0 - 5.6 style
                foreach (var item in classes.Skip(1))
                {
                    // skip lines with no destination package (a few random ones that clearly aren't classes)
                    if (item[3] != "")
                        AddToSide(item[4], LocalMappings, x => x.AddClass(item[3] + "/" + item[1], item[3] + "/" + item[0]));
                }
            }
        }
        var method_csv = zip.GetEntry("conf/methods.csv");
        if (method_csv != null)
        {
            var methods = ParseCSV(method_csv).ToList();
            if (methods[0].Length == 4)
            {
                // 6.0+ style
                foreach (var item in methods.Skip(1))
                {
                    AddToSide(item[2], GlobalMappings, x => x.AddMethod(item[0], item[1]));
                }
            }
            else if (methods[0].Length == 9)
            {
                // 3.0 - 5.6 style
                foreach (var item in methods.Skip(1))
                {
                    AddToSide(item[8], GlobalMappings, x => x.AddMethod(item[0], item[1]));
                    AddToSide(item[8], LocalMappings, x => x.AddMethod(item[7] + "/" + item[6] + "/" + item[2], item[0], item[4]));
                }
            }
            else
            {
                // 2.0 - 2.12 style
                // has some weird entries at the end we need to skip
                foreach (var item in methods.Skip(4).Where(x => x.Length >= 5))
                {
                    if (item[1] != "*" && item[1] != "")
                        GlobalMappings.Client.AddMethod(item[1], item[4]);
                    if (item[3] != "*" && item[3] != "")
                        GlobalMappings.Server.AddMethod(item[3], item[4]);
                }
            }
        }
        var fields_csv = zip.GetEntry("conf/fields.csv");
        if (fields_csv != null)
        {
            var fields = ParseCSV(fields_csv).ToList();
            if (fields[0].Length == 4)
            {
                // 6.0+ style
                foreach (var item in fields.Skip(1))
                {
                    AddToSide(item[2], GlobalMappings, x => x.AddField(item[0], item[1]));
                }
            }
            else if (fields[0].Length == 9)
            {
                // 3.0 - 5.6 style
                foreach (var item in fields.Skip(1))
                {
                    AddToSide(item[8], GlobalMappings, x => x.AddField(item[0], item[1]));
                    AddToSide(item[8], LocalMappings, x => x.AddField(item[7] + "/" + item[6] + "/" + item[2], item[0]));
                }
            }
            else
            {
                // 2.0 - 2.12 style
                foreach (var item in fields.Skip(3))
                {
                    if (item[2] != "*" && item[2] != "")
                        GlobalMappings.Client.AddField(item[2], item[6]);
                    if (item[5] != "*" && item[5] != "")
                        GlobalMappings.Server.AddField(item[5], item[6]);
                }
            }
        }
    }

    private IEnumerable<string[]> ParseCSV(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var parser = new TextFieldParser(reader);
        parser.HasFieldsEnclosedInQuotes = true;
        parser.SetDelimiters(",");
        while (!parser.EndOfData)
        {
            yield return parser.ReadFields();
        }
    }

    private void AddToSide<T>(string side, SidedMappings<T> mappings, Action<T> action) where T : new()
    {
        if (side == "0" || side == "2")
            action(mappings.Client);
        if (side == "1" || side == "2")
            action(mappings.Server);
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
