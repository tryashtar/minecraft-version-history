namespace MinecraftVersionHistory;

public class JavaConfig : VersionConfig
{
    public readonly List<string> JavaInstallationPaths;
    public readonly string FernflowerPath;
    public readonly string CfrPath;
    public readonly string SpecialSourcePath;
    public readonly string ServerJarFolder;
    public readonly string AssetsFolder;
    public readonly string DecompilerArgs;
    public readonly RetroMCP MCP;
    public readonly string CfrArgs;
    public readonly string FernflowerArgs;
    public readonly DateTime DataGenerators;
    public readonly DecompilerType? Decompiler;
    private readonly List<FileSorter> JsonSorters;
    private readonly List<Regex> ExcludeJarEntries;
    private readonly List<Regex> ExcludeDecompiledEntries;
    public JavaConfig(string folder, AppConfig parent, YamlMappingNode yaml) : base(folder, parent, yaml)
    {
        JavaInstallationPaths = yaml.Go("java install").ToList(x => Util.FilePath(folder, x));
        FernflowerPath = Util.FilePath(folder, yaml["fernflower jar"]);
        CfrPath = Util.FilePath(folder, yaml["cfr jar"]);
        SpecialSourcePath = Util.FilePath(folder, yaml["special source jar"]);
        ServerJarFolder = Util.FilePath(folder, yaml["server jars"]);
        AssetsFolder = Util.FilePath(folder, yaml["assets folder"]);
        string mcp = Util.FilePath(folder, yaml.Go("mcp", "merged"));
        string ver = yaml.Go("mcp", "matched").String();
        if (mcp != null && ver != null)
            MCP = new RetroMCP(mcp, ver);
        Decompiler = ParseDecompiler((string)yaml["decompiler"]);
        DecompilerArgs = (string)yaml["decompiler args"];
        CfrArgs = (string)yaml["cfr args"];
        FernflowerArgs = (string)yaml["fernflower args"];
        DataGenerators = DateTime.Parse((string)yaml["data generators"]);
        JsonSorters = new();
        foreach (YamlMappingNode entry in (YamlSequenceNode)yaml.Go("json sorting"))
        {
            var files = entry["file"];
            var list = new List<string>();
            if (files is YamlScalarNode single)
                list.Add(single.Value);
            else
                list.AddRange(((YamlSequenceNode)files).ToStringList());
            var sort = JsonSorterFactory.Create(entry["sort"]);
            var require = entry.Go("require").NullableParse(x => new SorterRequirements((YamlMappingNode)x)) ?? new SorterRequirements();
            JsonSorters.Add(new FileSorter(list.ToArray(), sort, require));
        }
        ExcludeJarEntries = yaml.Go("jar exclude").ToList(x => new Regex((string)x)) ?? new();
        ExcludeDecompiledEntries = yaml.Go("decompile exclude").ToList(x => new Regex((string)x)) ?? new();
    }

    public Sided<Mappings> GetMCPMappings(JavaVersion version)
    {
        if (MCP == null)
            return null;
        return MCP.CreateMappings(version.Name);
    }

    protected override VersionFacts CreateVersionFacts(YamlMappingNode yaml)
    {
        return new VersionFacts(yaml);
    }

    private static readonly string[] IllegalNames = new[] { "aux", "con", "nul", "prn", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
    public bool ExcludeJarEntry(string name)
    {
        if (IllegalNames.Contains(Path.GetFileNameWithoutExtension(name).ToLower()))
            return true;
        if (ExcludeJarEntries.Any(x => x.IsMatch(name)))
            return true;
        return false;
    }
    public bool ExcludeDecompiledEntry(string name)
    {
        if (ExcludeDecompiledEntries.Any(x => x.IsMatch(name)))
            return true;
        return false;
    }

    public void RemapJar(string in_path, string mappings_path, string out_path)
    {
        Profiler.Start("Remapping jar with SpecialSource");
        var result = CommandRunner.RunJavaCommand(Path.GetDirectoryName(out_path), JavaInstallationPaths, $"-jar \"{SpecialSourcePath}\" " +
            $"--in-jar \"{in_path}\" --out-jar \"{out_path}\" --srg-in \"{mappings_path}\" --kill-lvt");
        Profiler.Stop();
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"SpecialSource failed with exit code {result.ExitCode}: {result.Error}");
    }

    public void JsonSort(string folder, Version version)
    {
        foreach (var sorter in JsonSorters)
        {
            if (!sorter.Requirements.MetBy(version))
                continue;
            foreach (var f in sorter.Files)
            {
                var path = Path.Combine(folder, f);
                if (File.Exists(path))
                {
                    Console.WriteLine($"Sorting {f}");
                    if (!sorter.Requirements.MetBy(path))
                        continue;
                    SortJsonFile(path, sorter.Sorter);
                }
                else if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    Console.WriteLine($"Sorting {files.Length} files in {f}");
                    foreach (var sub in files)
                    {
                        if (!sorter.Requirements.MetBy(sub))
                            continue;
                        SortJsonFile(sub, sorter.Sorter);
                    }
                }
                else
                    Console.WriteLine($"Not sorting {f}, no file found");
            }
        }
    }

    private void SortJsonFile(string path, IJsonSorter sorter)
    {
        try
        {
            var json = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions() { });
            sorter.Sort(json);
            File.WriteAllText(path, Util.ToMinecraftJson(json));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Couldn't sort {path}: {ex.Message}");
        }
    }

    private static DecompilerType? ParseDecompiler(string input)
    {
        if (String.Equals(input, "fernflower", StringComparison.OrdinalIgnoreCase))
            return DecompilerType.Fernflower;
        if (String.Equals(input, "cfr", StringComparison.OrdinalIgnoreCase))
            return DecompilerType.Cfr;
        return null;
    }
}

public enum DecompilerType
{
    Fernflower,
    FernflowerUnzipped,
    Cfr
}
