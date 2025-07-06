namespace MinecraftVersionHistory;

public class PackMerger
{
    private readonly string LayersFolder;
    private readonly string OutputFolder;
    private readonly List<Regex> Exclude;
    private readonly List<string> Layers;
    private readonly List<MergingSpec> MergingSpecs;
    public PackMerger(YamlMappingNode node)
    {
        LayersFolder = (string)node.TryGet("input");
        OutputFolder = (string)node.TryGet("output");
        Exclude = node.Go("exclude").ToList(x => new Regex((string)x)) ?? new();
        Layers = node.Go("layers").ToStringList() ?? new List<string>();
        MergingSpecs = node.Go("merging").ToList(x => new MergingSpec((YamlMappingNode)x)) ?? new List<MergingSpec>();
    }

    public void Merge(string root)
    {
        Profiler.Start("Merging vanilla packs");
        string input_folder = Path.Combine(root, LayersFolder);
        string output_folder = Path.Combine(root, OutputFolder);
        Directory.CreateDirectory(output_folder);
        foreach (var layer in Layers)
        {
            if (layer == "vanilla_*")
            {
                var vanilla_slice_folders = Directory.GetDirectories(input_folder, "vanilla_*", SearchOption.TopDirectoryOnly);
                var vanilla_slices = new List<(int[], string)>();
                foreach (var folder in vanilla_slice_folders)
                {
                    var name = Path.GetFileName(folder);
                    if (!name.StartsWith("vanilla_"))
                        continue;
                    name = name.Substring("vanilla_".Length);
                    var points = name.Split('.');
                    int[] int_points;
                    try
                    {
                        int_points = points.Select(x => int.Parse(x)).ToArray();
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    vanilla_slices.Add((int_points, folder));
                }
                vanilla_slices.Sort((x, y) => CompareVersions(x.Item1, y.Item1));
                foreach (var slice in vanilla_slices)
                {
                    Console.WriteLine($"Applying {Path.GetFileName(slice.Item2)}");
                    MergeLayer(slice.Item2, output_folder);
                }
            }
            else
            {
                var pack = Path.Combine(input_folder, layer);
                if (!Directory.Exists(pack))
                {
                    Console.WriteLine($"Skipping {layer} (doesn't exist)");
                    continue;
                }

                Console.WriteLine($"Applying {layer}");
                MergeLayer(pack, output_folder);
            }
        }
        Profiler.Stop();
    }

    private int CompareVersions(int[] v1, int[] v2)
    {
        foreach (var (p1, p2) in v1.Zip(v2))
        {
            if (p1 > p2) return 1;
            if (p1 < p2) return -1;
        }
        if (v1.Length > v2.Length) return 1;
        if (v2.Length > v1.Length) return -1;
        return 0;
    }

    private void MergeLayer(string layer_folder, string output_folder)
    {
        foreach (var file in Directory.GetFiles(layer_folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(layer_folder, file);
            bool include = true;
            foreach (var exclude in Exclude)
            {
                if (exclude.IsMatch(relative))
                {
                    include = false;
                    break;
                }
            }

            if (!include)
            {
                continue;
            }
            var dest = Path.Combine(output_folder, relative);
            var specs = MergingSpecs.Where(x => x.Matches(relative)).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (specs.Any())
            {
                foreach (var spec in specs)
                {
                    spec.MergeFiles(dest, file);
                }
            }
            else
                Util.Copy(file, dest);
        }
    }
}
