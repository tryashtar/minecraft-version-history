namespace MinecraftVersionHistory;

public class ModernMCP : MCP
{
    public static IEnumerable<ModernMCP> LoadFrom(IEnumerable<string> srg_folders, IEnumerable<string> csv_folders)
    {
        var versions = new Dictionary<string, Dictionary<string, string>>();
        foreach (var folder in srg_folders.SelectMany(x => Directory.EnumerateDirectories(x, "*", SearchOption.AllDirectories)))
        {
            string tsrg = Path.Combine(folder, "joined.tsrg");
            string version_name = Path.GetFileName(folder);
            string series = Path.GetFileName(Path.GetDirectoryName(folder));
            if (File.Exists(tsrg))
            {
                if (!versions.ContainsKey(series))
                    versions.Add(series, new());
                versions[series][version_name] = tsrg;
            }
        }
        foreach (var folder in csv_folders.SelectMany(x => Directory.EnumerateDirectories(x, "*", SearchOption.AllDirectories)))
        {
            string category = Path.GetFileName(Path.GetDirectoryName(folder));
            string name = Path.GetFileName(folder);
            if (File.Exists(Path.Combine(folder, $"{category}-{name}.zip")))
            {
                string series_name = name[(name.IndexOf('-') + 1)..];
                if (versions.TryGetValue(series_name, out var items))
                {
                    var first = items.First();
                    yield return new ModernMCP(first.Key, first.Value, folder);
                    if (items.Count > 1)
                        items.Remove(first.Key);
                }
            }
        }
    }

    public readonly string ClientVersion;
    public readonly SidedMappings<TargetedMappings> Mappings = new();

    public ModernMCP(string mc_version, string tsrg_file, string csv_folder)
    {
        Console.WriteLine($"Loaded modern MCP for MC {mc_version}");
        ClientVersion = mc_version;
        ParseTSRG(tsrg_file, Mappings.Client);
        ParseTSRG(tsrg_file, Mappings.Server);
        StreamReader read(string path)
        {
            var file = Path.Combine(csv_folder, path);
            if (!File.Exists(file))
                return null;
            return File.OpenText(file);
        }
        ParseCSVs(Mappings,
            newids: read("newids.csv"),
            classes: read("classes.csv"),
            methods: read("methods.csv"),
            fields: read("fields.csv")
        );
    }

    private void ParseTSRG(string tsrg_file, TargetedMappings mappings)
    {
        using var reader = File.OpenText(tsrg_file);
        string current_class = null;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            if (line.StartsWith('\t'))
            {
                var entries = line[1..].Split(' ');
                if (entries.Length == 2)
                    mappings.AddField(current_class + "/" + entries[0], entries[1]);
                else if (entries.Length == 3)
                    mappings.AddMethod(current_class + "/" + entries[0], entries[2], entries[1]);
            }
            else
            {
                var entries = line.Split(' ');
                mappings.AddClass(entries[0], entries[1]);
                current_class = entries[0];
            }
        }
    }

    public override bool AcceptsVersion(JavaVersion version)
    {
        return ClientVersion == version.Name;
    }

    public override void CreateClientMappings(string path)
    {
        WriteSRG(path, Mappings.Client.Remap(GlobalMappings.Client).Remap(NewIDs.Client).Remap(GlobalMappings.Client));
    }

    public override void CreateServerMappings(string path)
    {
        WriteSRG(path, Mappings.Client.Remap(GlobalMappings.Client).Remap(NewIDs.Client).Remap(GlobalMappings.Client));
    }
}
