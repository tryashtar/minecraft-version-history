﻿namespace MinecraftVersionHistory;

public static class Util
{
    public static IEnumerable<VersionNode> OrderedChildren(this VersionNode node)
    {
        return node.Children.OrderBy(x => Depth(x));
    }

    public static string FilePath(string base_folder, YamlNode node, bool nullable = false)
    {
        if (nullable && node == null)
            return null;
        return Path.Combine(base_folder, Environment.ExpandEnvironmentVariables((string)node));
    }

    public static int Depth(this VersionNode node)
    {
        if (!node.Children.Any())
            return 1;
        return 1 + node.Children.Max(x => Depth(x));
    }

    public static string[] Split(string path)
    {
        return path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static void Copy(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to));
        File.Copy(from, to, true);
    }

    private static T PathToThing<T>(JObject root, Func<T> create_default, params string[] path) where T : JToken
    {
        JToken start = root;
        foreach (var item in path)
        {
            start = start[item];
            if (start == null)
                return create_default();
        }
        return start as T ?? create_default();
    }

    public static JObject PathToObject(JObject root, params string[] path) => PathToThing(root, () => new JObject(), path);
    public static JArray PathToArray(JObject root, params string[] path) => PathToThing(root, () => new JArray(), path);

    public static void RemoveEmptyFolders(string root)
    {
        foreach (var directory in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(directory) == ".git")
                continue;
            RemoveEmptyFolders(directory);
            if (Directory.GetFiles(directory).Length == 0 &&
                Directory.GetDirectories(directory).Length == 0)
            {
                Directory.Delete(directory, false);
            }
        }
    }

    public static void SortKeys(JObject obj, string[] order = null)
    {
        var tokens = new List<KeyValuePair<string, JToken>>();
        foreach (var item in obj)
        {
            tokens.Add(item);
        }
        obj.RemoveAll();
        var ordered = order == null ? tokens.OrderBy(x => x.Key) : tokens.OrderBy(x =>
        {
            var index = Array.IndexOf(order, x.Key);
            return index < 0 ? int.MaxValue : index;
        }).ThenBy(x => x.Key);
        foreach (var item in ordered)
        {
            obj.Add(item.Key, item.Value);
        }
    }

    public static string ToMinecraftJson(JToken value)
    {
        var sb = new StringBuilder(256);
        var sw = new StringWriter(sb, CultureInfo.InvariantCulture);

        var serializer = JsonSerializer.CreateDefault();
        using (var writer = new JsonTextWriter(sw))
        {
            writer.Formatting = Formatting.Indented;
            writer.IndentChar = ' ';
            writer.Indentation = 2;
            serializer.Serialize(writer, value, value.GetType());
        }

        return sw.ToString();
    }

    const int BYTES_TO_READ = sizeof(Int64);
    public static bool FilesAreEqual(FileInfo first, FileInfo second)
    {
        if (first.Length != second.Length)
            return false;

        if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
            return true;

        int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

        using (FileStream fs1 = first.OpenRead())
        using (FileStream fs2 = second.OpenRead())
        {
            byte[] one = new byte[BYTES_TO_READ];
            byte[] two = new byte[BYTES_TO_READ];

            for (int i = 0; i < iterations; i++)
            {
                fs1.Read(one, 0, BYTES_TO_READ);
                fs2.Read(two, 0, BYTES_TO_READ);

                if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                    return false;
            }
        }

        return true;
    }

    public static readonly HttpClient HTTP_CLIENT = new();
    public static void DownloadFile(string url, string path)
    {
        Profiler.Start($"Downloading {url} to {path}");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var stream = HTTP_CLIENT.GetStreamAsync(url).Result;
        using var file = File.Create(path);
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(file);
        Profiler.Stop();
    }
    public static string DownloadString(string url)
    {
        Profiler.Start($"Downloading {url}");
        string result = HTTP_CLIENT.GetStringAsync(url).Result;
        Profiler.Stop();
        return result;
    }
}