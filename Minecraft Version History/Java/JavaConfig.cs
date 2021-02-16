using System;
using YamlDotNet.RepresentationModel;

namespace Minecraft_Version_History
{
    public class JavaConfig
    {
        public readonly VersionFacts VersionFacts;
        public readonly string InputFolder;
        public readonly string OutputRepo;
        public readonly string JavaInstallationPath;
        public readonly string FernflowerPath;
        public readonly string CfrPath;
        public readonly string SpecialSourcePath;
        public readonly string ServerJarFolder;
        public readonly DecompilerType? Decompiler;
        public JavaConfig(YamlMappingNode yaml)
        {
            VersionFacts = new VersionFacts(yaml["version facts"] as YamlMappingNode);
            InputFolder = (string)yaml["version folder"];
            OutputRepo = (string)yaml["repo"];
            JavaInstallationPath = (string)yaml["java install"];
            FernflowerPath = (string)yaml["fernflower jar"];
            CfrPath = (string)yaml["cfr jar"];
            SpecialSourcePath = (string)yaml["special source jar"];
            ServerJarFolder = (string)yaml["server jars"];
            Decompiler = ParseDecompiler((string)yaml["decompiler"]);
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
}
