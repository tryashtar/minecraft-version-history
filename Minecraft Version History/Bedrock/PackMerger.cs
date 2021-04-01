using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
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
            Console.WriteLine("Merging vanilla packs");
            foreach (var layer in Layers)
            {
                var pack = Path.Combine(layer_folder, layer);
                if (!Directory.Exists(pack))
                {
                    Console.WriteLine($"Skipping {layer} (doesn't exist)");
                    continue;
                }
                Console.WriteLine($"Applying {layer}");
                foreach (var file in Directory.GetFiles(pack, "*.*", SearchOption.AllDirectories))
                {
                    var relative = Util.RelativePath(pack, file);
                    var dest = Path.Combine(output_folder, relative);
                    var specs = MergingSpecs.Where(x => x.Matches(relative));
                    if (specs.Any() && File.Exists(dest))
                    {
                        var current = JToken.Parse(File.ReadAllText(dest));
                        var newer = JToken.Parse(File.ReadAllText(file));
                        foreach (var spec in specs)
                        {
                            spec.Merge(current, newer);
                        }
                        File.WriteAllText(dest, Util.ToMinecraftJson(current));
                    }
                    else
                        Util.Copy(file, dest);
                }
            }
        }
    }
}
