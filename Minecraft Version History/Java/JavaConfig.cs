﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class JavaConfig : VersionConfig
    {
        public readonly string JavaInstallationPath;
        public readonly string FernflowerPath;
        public readonly string CfrPath;
        public readonly string SpecialSourcePath;
        public readonly string ServerJarFolder;
        public readonly string DecompilerArgs;
        public readonly string CfrArgs;
        public readonly string FernflowerArgs;
        public readonly DateTime DataGenerators;
        public readonly DateTime AssetGenerators;
        public readonly DecompilerType? Decompiler;
        private readonly Dictionary<string, JsonSorter> JsonSorters;
        private readonly List<Regex> ExcludeJarEntries;
        public JavaConfig(string folder, YamlMappingNode yaml) : base(yaml)
        {
            JavaInstallationPath = Path.Combine(folder, (string)yaml["java install"]);
            FernflowerPath = Path.Combine(folder, (string)yaml["fernflower jar"]);
            CfrPath = Path.Combine(folder, (string)yaml["cfr jar"]);
            SpecialSourcePath = Path.Combine(folder, (string)yaml["special source jar"]);
            ServerJarFolder = Path.Combine(folder, (string)yaml["server jars"]);
            Decompiler = ParseDecompiler((string)yaml["decompiler"]);
            DecompilerArgs = (string)yaml["decompiler args"];
            CfrArgs = (string)yaml["cfr args"];
            FernflowerArgs = (string)yaml["fernflower args"];
            DataGenerators = DateTime.Parse((string)yaml["data generators"]);
            AssetGenerators = DateTime.Parse((string)yaml["asset generators"]);
            JsonSorters = yaml.Go("json sorting").ToDictionary(x => (string)x, x => new JsonSorter((YamlMappingNode)x)) ?? new Dictionary<string, JsonSorter>();
            ExcludeJarEntries = yaml.Go("jar exclude").ToList(x => new Regex((string)x)) ?? new List<Regex>();
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

        public bool NeedsJsonSorting(string name)
        {
            return JsonSorters.ContainsKey(name);
        }

        public void JsonSort(string name, JObject json)
        {
            JsonSorters[name].Sort(json);
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
