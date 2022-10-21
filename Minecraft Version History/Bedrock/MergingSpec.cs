namespace MinecraftVersionHistory;

public class MergingSpec
{
    private readonly string Extension;
    private readonly string[] Path;
    private readonly ForwardNodeFinder ListPath;
    private readonly List<string> OverwriteKeys;
    public readonly MergeOperation Operation;
    public readonly KeyMover KeyMover;
    public MergingSpec(YamlMappingNode node)
    {
        var path_node = node.TryGet("path");
        if (path_node != null)
            Path = Util.Split((string)path_node);
        var list_path_node = node.TryGet("list");
        if (list_path_node != null)
            ListPath = new ForwardNodeFinder(list_path_node.ToList(NodeMatcher.Create));
        var ext_node = node.TryGet("extension");
        if (ext_node != null)
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
            Operation = StringUtils.ParseUnderscoredEnum<MergeOperation>((string)operation_node);
        KeyMover = node.Go("move_keys").NullableParse(x => new KeyMover((YamlMappingNode)x));
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
            var newer = JsonNode.Parse(File.ReadAllText(newer_path), null, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip });
            if (KeyMover != null && newer is JsonObject obj)
                KeyMover.MoveKeys(obj);
            JsonNode result = newer;
            if (File.Exists(current_path))
            {
                result = JsonNode.Parse(File.ReadAllText(current_path));
                TopLevelMerge(result, newer);
            }
            File.WriteAllText(current_path, Util.ToMinecraftJson(result));
        }
        else if (Operation == MergeOperation.AppendList)
        {
            var newer = JsonNode.Parse(File.ReadAllText(newer_path), null, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip });
            JsonNode result = newer;
            if (File.Exists(current_path))
            {
                result = JsonNode.Parse(File.ReadAllText(current_path));
                var result_lists = ListPath.FindNodes(result).Select(x => x.node).OfType<JsonArray>().ToList();
                var newer_lists = ListPath.FindNodes(newer).Select(x => x.node).OfType<JsonArray>().ToList();
                foreach (var r in result_lists)
                {
                    foreach (var n in newer_lists)
                    {
                        MergeTopArray(r, n);
                    }
                }
            }
            File.WriteAllText(current_path, Util.ToMinecraftJson(result));
        }
        else if (Operation == MergeOperation.AppendLines)
        {
            if (File.Exists(current_path))
            {
                using var input = File.OpenRead(newer_path);
                using var output = new FileStream(current_path, FileMode.Append, FileAccess.Write, FileShare.None);
                output.Write(Encoding.UTF8.GetBytes(Environment.NewLine));
                input.CopyTo(output);
            }
            else
                File.Copy(newer_path, current_path);
        }
        else if (Operation == MergeOperation.NoMerge)
        {
            if (!File.Exists(current_path))
                File.Copy(newer_path, current_path);
        }
    }

    public void TopLevelMerge(JsonNode current, JsonNode newer)
    {
        if (current is JsonObject cj && newer is JsonObject nj)
            MergeObjects(cj, nj);
        else if (current is JsonArray ca && newer is JsonArray na)
            MergeTopArray(ca, na);
    }

    public void MergeObjects(JsonObject current, JsonObject newer)
    {
        foreach (var item in newer)
        {
            bool exists = current.TryGetPropertyValue(item.Key, out var existing);
            if (!exists || OverwriteKeys.Contains(item.Key))
                current[item.Key] = JsonNode.Parse(item.Value.ToJsonString());
            else
            {
                if (existing is JsonObject j1 && item.Value is JsonObject j2)
                    MergeObjects(j1, j2);
                else
                    current[item.Key] = JsonNode.Parse(item.Value.ToJsonString());
            }
        }
    }

    public void MergeTopArray(JsonArray current, JsonArray newer)
    {
        foreach (var sub in newer)
        {
            current.Add(JsonNode.Parse(sub.ToJsonString()));
        }
    }
}

public enum MergeOperation
{
    AppendLines,
    MergeJson,
    AppendList,
    NoMerge
}
