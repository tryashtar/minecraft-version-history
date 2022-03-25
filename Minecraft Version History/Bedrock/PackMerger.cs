namespace MinecraftVersionHistory;

public class PackMerger
{
    private readonly List<string> Layers;
    private readonly List<MergingSpec> MergingSpecs;
    public PackMerger(YamlMappingNode node)
    {
        Layers = node.Go("layers").ToStringList() ?? new List<string>();
        MergingSpecs = node.Go("merging").ToList(x => new MergingSpec((YamlMappingNode)x)) ?? new List<MergingSpec>();
    }

    public void Merge(string layer_folder, string output_folder)
    {
        Profiler.Start("Merging vanilla packs");
        foreach (var layer in Layers)
        {
            var pack = Path.Combine(layer_folder, layer);
            if (!Directory.Exists(pack))
            {
                Console.WriteLine($"Skipping {layer} (doesn't exist)");
                continue;
            }
            Console.WriteLine($"Applying {layer}");
            foreach (var file in Directory.GetFiles(pack, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(pack, file);
                var dest = Path.Combine(output_folder, relative);
                var specs = MergingSpecs.Where(x => x.Matches(relative));
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
        Profiler.Stop();
    }
}
