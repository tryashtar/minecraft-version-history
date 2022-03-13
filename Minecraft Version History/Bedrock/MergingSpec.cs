namespace MinecraftVersionHistory;

public class MergingSpec
{
    private readonly string Extension;
    private readonly string[] Path;
    private readonly List<string> OverwriteKeys;
    public readonly MergeOperation Operation;
    public MergingSpec(YamlMappingNode node)
    {
        var path_node = node.TryGet("path");
        if (path_node == null)
            Path = null;
        else
            Path = Util.Split((string)path_node);
        var ext_node = node.TryGet("extension");
        if (ext_node == null)
            Extension = null;
        else
        {
            Extension = (string)ext_node;
            if (!Extension.StartsWith('.'))
                Extension = "." + Extension;
        }
        OverwriteKeys = node.Go("overwrite").ToStringList() ?? new List<string>();
        var operation_node = node.TryGet("operation");
        if (operation_node == null)
            Operation = MergeOperation.MergeJson;
        else
            Operation = ParseMergeOperation((string)operation_node);
    }

    public bool Matches(string path)
    {
        if (Path != null)
        {
            var split = Util.Split(path);
            if (split.Length < Path.Length)
                return false;
            for (int i = 0; i < Path.Length; i++)
            {
                if (split[i] != Path[i])
                    return false;
            }
        }
        if (Extension != null)
        {
            if (System.IO.Path.GetExtension(path) != Extension)
                return false;
        }
        return true;
    }

    public void MergeFiles(string current_path, string newer_path)
    {
        if (Operation == MergeOperation.MergeJson)
        {
            var current = JToken.Parse(File.ReadAllText(current_path));
            var newer = JToken.Parse(File.ReadAllText(newer_path));
            TopLevelMerge(current, newer);
            File.WriteAllText(current_path, Util.ToMinecraftJson(current));
        }
        else if (Operation == MergeOperation.AppendLines)
        {
            using Stream input = File.OpenRead(newer_path);
            using Stream output = new FileStream(current_path, FileMode.Append, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    public void TopLevelMerge(JToken current, JToken newer)
    {
        if (current is JObject cj && newer is JObject nj)
            MergeObjects(cj, nj);
        else if (current is JArray ca && newer is JArray na)
            MergeTopArray(ca, na);
    }

    public void MergeObjects(JObject current, JObject newer)
    {
        foreach (var item in newer)
        {
            bool exists = current.TryGetValue(item.Key, out var existing);
            if (!exists || OverwriteKeys.Contains(item.Key))
                current[item.Key] = item.Value;
            else
            {
                if (existing is JObject j1 && item.Value is JObject j2)
                    MergeObjects(j1, j2);
                else
                    current[item.Key] = item.Value;
            }
        }
    }

    public void MergeTopArray(JArray current, JArray newer)
    {
        foreach (var sub in newer)
        {
            current.Add(sub);
        }
    }

    private static MergeOperation ParseMergeOperation(string str)
    {
        if (String.Equals(str, "append_lines", StringComparison.OrdinalIgnoreCase))
            return MergeOperation.AppendLines;
        else if (String.Equals(str, "merge_json", StringComparison.OrdinalIgnoreCase))
            return MergeOperation.MergeJson;
        throw new ArgumentException($"Unknown merge operation: {str}");
    }
}

public enum MergeOperation
{
    AppendLines,
    MergeJson
}
