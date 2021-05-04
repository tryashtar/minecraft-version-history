using fNbt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TryashtarUtils.Nbt;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class NbtTranslationOptions
    {
        public readonly string Extension;
        public readonly string NewExtension;
        public readonly Endianness Endianness;
        public readonly bool Minified;
        private readonly List<string> RemoveKeys;
        public NbtTranslationOptions(YamlMappingNode node)
        {
            Extension = (string)node["extension"];
            if (!Extension.StartsWith('.'))
                Extension = "." + Extension;
            RemoveKeys = node.Go("remove keys").ToStringList() ?? new List<string>();
            var endian_node = node.TryGet("endian");
            if (endian_node == null)
                Endianness = Endianness.Big;
            else
                Endianness = ParseEndianness((string)endian_node);
            var new_ext_node = node.TryGet("new extension");
            if (new_ext_node == null)
                NewExtension = ".snbt";
            else
                NewExtension = (string)new_ext_node;
            var mini_node = node.TryGet("minified");
            if (mini_node == null)
                Minified = false;
            else
                Minified = Boolean.Parse((string)mini_node);
        }

        public bool ShouldTranslate(string path)
        {
            return Path.GetExtension(path) == Extension;
        }

        public void Translate(string path)
        {
            var file = new NbtFile();
            if (Endianness == Endianness.Little)
                file.BigEndian = false;
            file.LoadFromFile(path);
            if (RemoveKeys.Any())
            {
                foreach (var key in RemoveKeys)
                {
                    ((NbtCompound)file.RootTag).Remove(key);
                }
                file.SaveToFile(path, file.FileCompression);
            }
            SnbtOptions options = Minified ? SnbtOptions.Default : SnbtOptions.DefaultExpanded;
            File.WriteAllText(Path.ChangeExtension(path, NewExtension), file.RootTag.ToSnbt(options) + "\n");
        }

        private static Endianness ParseEndianness(string str)
        {
            if (String.Equals(str, "big", StringComparison.OrdinalIgnoreCase))
                return Endianness.Big;
            else if (String.Equals(str, "little", StringComparison.OrdinalIgnoreCase))
                return Endianness.Little;
            throw new ArgumentException($"Invalid endianness: {str}");
        }
    }

    public enum Endianness
    {
        Big,
        Little
    }
}
