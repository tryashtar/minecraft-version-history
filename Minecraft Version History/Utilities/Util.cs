using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace MinecraftVersionHistory;

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
        string path = (string)node;
        path = Environment.ExpandEnvironmentVariables(path);
        return Path.Combine(base_folder, path);
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

    private static T PathToThing<T>(JsonObject root, Func<T> create_default, params string[] path) where T : JsonNode
    {
        JsonNode start = root;
        foreach (var item in path)
        {
            start = start[item];
            if (start == null)
                return create_default();
        }
        return start as T ?? create_default();
    }

    public static JsonObject PathToObject(JsonObject root, params string[] path) => PathToThing(root, () => new JsonObject(), path);
    public static JsonArray PathToArray(JsonObject root, params string[] path) => PathToThing(root, () => new JsonArray(), path);

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

    public static string ToMinecraftJson(JsonNode value)
    {
        return value.ToJsonString(new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
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
