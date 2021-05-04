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
        public readonly string InputFolder;
        public readonly string OutputRepo;
        public readonly VersionFacts VersionFacts;
        public readonly List<NbtTranslationOptions> NbtTranslations;
        public VersionConfig(YamlMappingNode yaml)
        {
            InputFolder = (string)yaml["version folder"];
            OutputRepo = (string)yaml["repo"];
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
