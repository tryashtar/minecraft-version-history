using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public abstract class VersionConfig
    {
        public readonly List<string> InputFolders;
        public readonly string OutputRepo;
        public readonly VersionFacts VersionFacts;
        public readonly List<NbtTranslationOptions> NbtTranslations;
        public VersionConfig(string folder, YamlMappingNode yaml)
        {
            InputFolders = yaml.Go("version folders").ToList(x => Util.FilePath(folder, x));
            OutputRepo = Util.FilePath(folder, yaml["repo"]);
            VersionFacts = CreateVersionFacts(yaml["version facts"] as YamlMappingNode);
            NbtTranslations = yaml.Go("nbt translations").ToList(x => new NbtTranslationOptions((YamlMappingNode)x)) ?? new List<NbtTranslationOptions>();
        }

        protected abstract VersionFacts CreateVersionFacts(YamlMappingNode yaml);

        public void TranslateNbt(string path)
        {
            foreach (var mode in NbtTranslations)
            {
                if (mode.ShouldTranslate(path))
                    mode.Translate(path);
            }
        }
    }
}
