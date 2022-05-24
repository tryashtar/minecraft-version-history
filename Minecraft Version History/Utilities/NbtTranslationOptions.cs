namespace MinecraftVersionHistory;

public class NbtTranslationOptions
{
    public readonly string Extension;
    public readonly string NewExtension;
    public readonly Endianness Endianness;
    public readonly SnbtOptions Options;
    public readonly bool ConvertStructure;
    private readonly List<string> RemoveKeys;
    private readonly bool Mini;
    public NbtTranslationOptions(YamlMappingNode node)
    {
        Extension = (string)node["extension"];
        if (!Extension.StartsWith('.'))
            Extension = "." + Extension;
        RemoveKeys = node.Go("remove keys").ToStringList() ?? new List<string>();
        Endianness = node.Go("endian").ToEnum<Endianness>() ?? Endianness.Big;
        NewExtension = node.TryGet("new extension").String() ?? ".snbt";
        Mini = node.Go("minified").NullableStructParse(x => Boolean.Parse(x.String())) ?? false;
        Options = Mini ? SnbtOptions.Default : SnbtOptions.DefaultExpanded;
        ConvertStructure = node.Go("structure").NullableStructParse(x => Boolean.Parse(x.String())) ?? false;
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
        var root = (NbtCompound)file.RootTag;
        if (RemoveKeys.Any())
        {
            foreach (var key in RemoveKeys)
            {
                root.Remove(key);
            }
            file.SaveToFile(path, file.FileCompression);
        }
        if (ConvertStructure)
        {
            var palettes = root.Get<NbtList>("palettes");
            var palette = palettes != null ? (NbtList)palettes[0] : root.Get<NbtList>("palette");
            var new_palette = new NbtList(palette.Tags.Cast<NbtCompound>().Select(PackBlockState));
            root["palette"] = new_palette;
            if (palettes != null)
            {
                var new_palettes = new NbtList();
                foreach (NbtList p in palettes)
                {
                    var compound = new NbtCompound();
                    for (int i = 0; i < palettes.Count; i++)
                    {
                        compound[i.ToString()] = PackBlockState((NbtCompound)p[i]);
                    }
                }
                root["palettes"] = new_palettes;
            }
            var entities = root.Get<NbtList>("entities");
            if (entities != null)
            {
                var sorted = entities.Tags.Cast<NbtCompound>()
                    .OrderBy(x => x.Get<NbtList>("pos")[1].DoubleValue)
                    .ThenBy(x => x.Get<NbtList>("pos")[0].DoubleValue)
                    .ThenBy(x => x.Get<NbtList>("pos")[2].DoubleValue).ToList();
                entities.Clear();
                root["entities"] = new NbtList(sorted);
            }
            var blocks = root.Get<NbtList>("blocks");
            var new_blocks = blocks.Tags.Cast<NbtCompound>()
                .OrderBy(x => x.Get<NbtList>("pos")[1].IntValue)
                .ThenBy(x => x.Get<NbtList>("pos")[0].IntValue)
                .ThenBy(x => x.Get<NbtList>("pos")[2].IntValue).ToList();
            blocks.Clear();
            foreach (var block in new_blocks)
            {
                var state = block.Get<NbtInt>("state");
                block["state"] = (NbtTag)new_palette[state.Value].Clone();
                block.Sort(new BlockOrder(), false);
            }
            root["blocks"] = new NbtList(new_blocks);
            root["blocks"].Name = "data";
        }
        root.Sort(new StructureOrder(), false);
        if (!Mini)
        {
            Options.ShouldIndent = tag =>
            {
                if (tag.Parent == null || tag.Parent == root)
                    return true;
                var top_parent = tag;
                while (top_parent.Parent != root)
                {
                    top_parent = top_parent.Parent;
                }
                if (top_parent == root["data"])
                    return false;
                if (top_parent == root["entities"])
                    return false;
                return true;
            };
        }
        File.WriteAllText(Path.ChangeExtension(path, NewExtension), root.ToSnbt(Options) + Environment.NewLine);
    }

    private class StructureOrder : IComparer<NbtTag>
    {
        public int Compare(NbtTag x, NbtTag y)
        {
            return Index(x.Name).CompareTo(Index(y.Name));
        }
        private int Index(string name)
        {
            if (name == "size")
                return 0;
            if (name == "data")
                return 1;
            if (name == "entities")
                return 2;
            if (name == "palette")
                return 3;
            return 4;
        }
    }
    private class BlockOrder : IComparer<NbtTag>
    {
        public int Compare(NbtTag x, NbtTag y)
        {
            return Index(x.Name).CompareTo(Index(y.Name));
        }
        private int Index(string name)
        {
            if (name == "pos")
                return 0;
            if (name == "state")
                return 1;
            if (name == "nbt")
                return 2;
            return 3;
        }
    }

    // differs slightly from the Minecraft implementation
    // emits block states in the form [key=value] instead of {key:value}
    private NbtString PackBlockState(NbtCompound compound)
    {
        var builder = new StringBuilder(compound.Get<NbtString>("Name").Value);
        var properties = compound.Get<NbtCompound>("Properties");
        if (properties != null)
        {
            string str = String.Join(',', properties.Select(x => x.Name + '=' + x.StringValue));
            builder.Append('[').Append(str).Append(']');
        }
        return new NbtString(builder.ToString());
    }
}

public enum Endianness
{
    Big,
    Little
}
