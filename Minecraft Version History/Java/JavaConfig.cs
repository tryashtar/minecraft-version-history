namespace MinecraftVersionHistory;

public class JavaConfig : VersionConfig
{
    public readonly List<string> JavaInstallationPaths;
    public readonly string FernflowerPath;
    public readonly string CfrPath;
    public readonly string SpecialSourcePath;
    public readonly string ServerJarFolder;
    public readonly string DecompilerArgs;
    public readonly string CfrArgs;
    public readonly string FernflowerArgs;
    public readonly DateTime DataGenerators;
    public readonly DecompilerType? Decompiler;
    private readonly Dictionary<string, IJsonSorter> JsonSorters;
    private readonly List<Regex> ExcludeJarEntries;
    private readonly List<Regex> ExcludeDecompiledEntries;
    public JavaConfig(string folder, YamlMappingNode yaml) : base(folder, yaml)
    {
        JavaInstallationPaths = yaml.Go("java install").ToList(x => Util.FilePath(folder, x));
        FernflowerPath = Util.FilePath(folder, yaml["fernflower jar"]);
        CfrPath = Util.FilePath(folder, yaml["cfr jar"]);
        SpecialSourcePath = Util.FilePath(folder, yaml["special source jar"]);
        ServerJarFolder = Util.FilePath(folder, yaml["server jars"]);
        Decompiler = ParseDecompiler((string)yaml["decompiler"]);
        DecompilerArgs = (string)yaml["decompiler args"];
        CfrArgs = (string)yaml["cfr args"];
        FernflowerArgs = (string)yaml["fernflower args"];
        DataGenerators = DateTime.Parse((string)yaml["data generators"]);
        JsonSorters = yaml.Go("json sorting").ToDictionary(x => (string)x, JsonSorterFactory.Create) ?? new();
        ExcludeJarEntries = yaml.Go("jar exclude").ToList(x => new Regex((string)x)) ?? new();
        ExcludeDecompiledEntries = yaml.Go("decompile exclude").ToList(x => new Regex((string)x)) ?? new();
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

    public void JsonSort(string folder, Version version)
    {
        foreach (var (key, sort) in JsonSorters)
        {
            if (!sort.ShouldSort(version))
                continue;
            var path = Path.Combine(folder, key);
            if (File.Exists(path))
            {
                Console.WriteLine($"Sorting {key}");
                SortJsonFile(path, sort);
            }
            else if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path);
                Console.WriteLine($"Sorting {files.Length} files in {key}");
                foreach (var sub in files)
                {
                    SortJsonFile(sub, sort);
                }
            }
            else
                Console.WriteLine($"Not sorting {key}, no file found");
        }
    }

    private void SortJsonFile(string path, IJsonSorter sorter)
    {
        var json = JObject.Parse(File.ReadAllText(path));
        sorter.Sort(json);
        File.WriteAllText(path, Util.ToMinecraftJson(json));
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
    Cfr
}
