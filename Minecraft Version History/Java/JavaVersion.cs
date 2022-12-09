using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MinecraftVersionHistory;

public class JavaVersion : Version
{
    public EndpointData Server { get; private set; }
    public EndpointData Client { get; private set; }
    public readonly string AssetsURL;
    public readonly string ServerJarURL;
    public readonly string LauncherJsonPath;
    public JavaVersion(string folder, VersionFacts facts)
    {
        Name = Path.GetFileName(folder);
        Name = facts.CustomName(Name) ?? Name;
        LauncherJsonPath = Path.Combine(folder, Name + ".json");
        var json = JsonObject.Parse(File.ReadAllText(LauncherJsonPath));
        ReleaseTime = DateTime.Parse(json["releaseTime"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None);
        Client = new(
            "client",
            Path.Combine(folder, Name + ".jar"),
            (string)json["downloads"]?["client_mappings"]?["url"]
        );
        Server = new(
            "server",
            null,
            (string)json["downloads"]?["server_mappings"]?["url"]
        );
        ServerJarURL = (string)json["downloads"]?["server"]?["url"];
        AssetsURL = (string)json["assetIndex"]?["url"];
    }

    public static bool LooksValid(string folder)
    {
        string name = Path.GetFileName(folder);
        string jsonpath = Path.Combine(folder, name + ".json");
        string jarpath = Path.Combine(folder, name + ".jar");
        return File.Exists(jsonpath) && File.Exists(jarpath);
    }

    private void RunDataGenerators(JavaConfig config, string folder)
    {
        string reports_path = Path.Combine(config.ServerJarFolder, "generated");
        if (Directory.Exists(reports_path))
            Directory.Delete(reports_path, true);

        DownloadServerJar(config);
        if (Server.JarPath is not null)
        {
            Profiler.Start("Fetching data reports");
            string args1 = $"-cp \"{Server.JarPath}\" net.minecraft.data.Main --reports";
            string args2 = $"-DbundlerMainClass=net.minecraft.data.Main -jar \"{Server.JarPath}\" --reports";
            var result = CommandRunner.RunJavaCombos(
                config.ServerJarFolder,
                config.JavaInstallationPaths,
                new[] { args1, args2 }
            );
            if (result.ExitCode != 0)
                throw new ApplicationException("Failed to get data reports");
            Directory.CreateDirectory(folder);
            FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), folder);
            if (Directory.Exists(reports_path))
                Directory.Delete(reports_path, true);
            Profiler.Stop();
        }
    }

    private void ExtractJar(JavaConfig config, string folder)
    {
        Profiler.Start($"Extracting jar");
        using ZipArchive zip = ZipFile.OpenRead(Client.JarPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith("/") || config.ExcludeJarEntry(entry.FullName))
                continue;
            Directory.CreateDirectory(Path.Combine(folder, Path.GetDirectoryName(entry.FullName)));
            var destination = Path.Combine(folder, entry.FullName);
            entry.ExtractToFile(destination);
        }
        Profiler.Stop();
    }

    private void FetchAssets(JavaConfig config, string json_path, string folder)
    {
        Profiler.Start("Fetching assets");
        var assets_file = Util.DownloadString(AssetsURL);
        File.WriteAllText(json_path, assets_file);
        var json = JsonObject.Parse(assets_file);
        foreach ((string path, JsonNode data) in (JsonObject)json["objects"])
        {
            var hash = (string)data["hash"];
            var cached = Path.Combine(config.AssetsFolder, "objects", hash[0..2], hash);
            var destination = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(cached))
                File.Copy(cached, destination, true);
            else
                Util.DownloadFile($"https://resources.download.minecraft.net/{hash[0..2]}/{hash}", destination);
        }
        Profiler.Stop();
    }

    public override void ExtractData(string folder, AppConfig config)
    {
        var java_config = config.Java;
        var json_exporting = new List<Task>();
        var steps = new List<Task>();
        static Task step(Action action)
        {
            var task = Task.Run(action);
            task.ContinueWith(x =>
            {
                if (x.IsFaulted)
                    throw x.Exception;
            });
            return task;
        }

        if (ReleaseTime > java_config.DataGenerators)
            json_exporting.Add(step(() => RunDataGenerators(java_config, Path.Combine(folder, "reports"))));
        if (AssetsURL != null)
            json_exporting.Add(step(() => FetchAssets(java_config, Path.Combine(folder, "assets.json"), Path.Combine(folder, "assets"))));
        json_exporting.Add(step(() => ExtractJar(java_config, Path.Combine(folder, "jar"))));
        steps.Add(step(() => DecompileMinecraft(java_config, Path.Combine(folder, "source"))));

        Task.WaitAll(json_exporting.ToArray());
        java_config.JsonSort(folder, this);
        Task.WaitAll(steps.ToArray());
        File.Copy(LauncherJsonPath, Path.Combine(folder, "launcher.json"));
    }

    public record EndpointData(string Name, string JarPath, string MappingsURL);

    private string MapJar(JavaConfig config, EndpointData side, string folder)
    {
        string mappings_path = Path.Combine(Path.GetDirectoryName(folder), $"mappings_{side.Name}.txt");
        if (side.MappingsURL != null)
            Util.DownloadFile(side.MappingsURL, mappings_path);
        else
        {
            var mcp = config.GetMCPMappings(this);
            if (mcp == null)
                return null;
            Profiler.Start("Using MCP mappings");
            using (var writer = new StreamWriter(mappings_path))
            {
                if (side.Name == "server")
                    MappingsIO.WriteTsrg(mcp.Server, writer);
                else if (side.Name == "client")
                    MappingsIO.WriteTsrg(mcp.Client, writer);
                else
                    mcp = null;
            }
            Profiler.Stop();
            if (mcp == null)
                return null;
        }
        string mapped_jar_path = Path.Combine(folder, $"mapped_{side.Name}.jar");
        config.RemapJar(side.JarPath, mappings_path, mapped_jar_path);
        return mapped_jar_path;
    }

    private void DecompileJar(JavaConfig config, string jar_path, string folder)
    {
        Profiler.Start($"Decompiling");
        if (config.Decompiler == DecompilerType.Cfr)
        {
            var result = CommandRunner.RunJavaCommand(folder, config.JavaInstallationPaths, $"{config.DecompilerArgs} -jar \"{config.CfrPath}\" \"{jar_path}\" " +
                $"--outputdir \"{folder}\" {config.CfrArgs}");
            if (result.ExitCode != 0)
                throw new ApplicationException($"Failed to decompile: {result.Error}");
            string summary_file = Path.Combine(folder, "summary.txt");
            if (File.Exists(summary_file))
            {
                Console.WriteLine("Summary:");
                Console.WriteLine(File.ReadAllText(summary_file));
                File.Delete(summary_file);
            }
        }
        else if (config.Decompiler == DecompilerType.Fernflower)
        {
            string output_dir = Path.Combine(folder, "decompiled");
            Directory.CreateDirectory(output_dir);
            var result = CommandRunner.RunJavaCommand(folder, config.JavaInstallationPaths, $"{config.DecompilerArgs} -jar \"{config.FernflowerPath}\" " +
                 $"{config.FernflowerArgs} \"{jar_path}\" \"{output_dir}\""); ;
            if (result.ExitCode != 0)
                throw new ApplicationException($"Failed to decompile: {result.Error}");
            using (ZipArchive zip = ZipFile.OpenRead(Path.Combine(output_dir, Path.GetFileName(jar_path))))
            {
                foreach (var entry in zip.Entries)
                {
                    Directory.CreateDirectory(Path.Combine(folder, Path.GetDirectoryName(entry.FullName)));
                    entry.ExtractToFile(Path.Combine(folder, entry.FullName));
                }
            }
            Directory.Delete(output_dir, true);
        }
        else
            throw new ArgumentException(nameof(config.Decompiler));
        Profiler.Stop();
    }

    private string[] ReadClassPath(ZipArchive archive)
    {
        var entry = archive.GetEntry("META-INF/classpath-joined");
        if (entry != null)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var files = reader.ReadToEnd().Split(";");
            return files;
        }
        return null;
    }

    private void DecompileMinecraft(JavaConfig config, string destination)
    {
        Directory.CreateDirectory(destination);
        string final_jar = Path.Combine(destination, $"{Path.GetFileNameWithoutExtension(Client.JarPath)}_final.jar");
        string mapped_client = MapJar(config, Client, destination);
        string used_client = mapped_client ?? Client.JarPath;
        string mapped_server = null;
        string unbundled_server_path = null;
        DownloadServerJar(config);
        string final_server_jar = Server.JarPath;
        if (Server.JarPath is not null)
        {
            using (ZipArchive archive = ZipFile.Open(Server.JarPath, ZipArchiveMode.Read))
            {
                var libraries = ReadClassPath(archive);
                if (libraries != null)
                {
                    Profiler.Start("Unbundling server");

                    unbundled_server_path = Path.Combine(destination, $"{Path.GetFileNameWithoutExtension(Server.JarPath)}_unbundled.jar");
                    final_server_jar = unbundled_server_path;
                    using ZipArchive unbundled = ZipFile.Open(unbundled_server_path, ZipArchiveMode.Create);

                    foreach (var library in libraries)
                    {
                        var bundled_jar = archive.GetEntry("META-INF/" + library);
                        using var jar_stream = bundled_jar.Open();
                        using var jar_archive = new ZipArchive(jar_stream);
                        CombineArchives(jar_archive, unbundled);
                    }

                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.FullName.StartsWith("META-INF/"))
                            CopyEntry(entry, unbundled);
                    }

                    Profiler.Stop();
                }
            }
            mapped_server = MapJar(config, Server with { JarPath = final_server_jar }, destination);
            string used_server = mapped_server ?? final_server_jar;
            CombineJars(final_jar, used_client, used_server);
        }
        else
            File.Copy(used_client, final_jar);

        Profiler.Start("Processing final jar files");
        // use the old-style using since the zip needs to dispose before decompiling starts
        using (ZipArchive archive = ZipFile.Open(final_jar, ZipArchiveMode.Update))
        {
            foreach (var entry in archive.Entries.ToList())
            {
                if (entry.FullName.EndsWith("/"))
                    continue;
                if (config.ExcludeDecompiledEntry(entry.FullName))
                    entry.Delete();
            }
        }
        Profiler.Stop();

        DecompileJar(config, final_jar, destination);

        if (mapped_client is not null)
            File.Delete(mapped_client);
        if (mapped_server is not null)
            File.Delete(mapped_server);
        if (unbundled_server_path is not null)
            File.Delete(unbundled_server_path);
        File.Delete(final_jar);
    }

    private void CombineArchives(ZipArchive source_zip, ZipArchive destination)
    {
        foreach (var item in source_zip.Entries)
        {
            CopyEntry(item, destination);
        }
    }

    private void CopyEntry(ZipArchiveEntry entry, ZipArchive destination)
    {
        var file = destination.CreateEntry(entry.FullName);
        using var source = entry.Open();
        using var dest = file.Open();
        source.CopyTo(dest);
    }

    private void CombineJars(string destination, params string[] paths)
    {
        Profiler.Start($"Combining {paths.Length} jar files");
        using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
        foreach (var path in paths)
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            CombineArchives(zip, archive);
        }
        Profiler.Stop();
    }

    public void DownloadServerJar(JavaConfig config)
    {
        if (ServerJarURL is null)
            return;
        string path = Path.Combine(config.ServerJarFolder, Name + ".jar");
        if (Server.JarPath is null)
            Server = Server with { JarPath = path };
        if (!File.Exists(path))
        {
            Util.DownloadFile(ServerJarURL, path);
            Server = Server with { JarPath = path };
        }
    }
}
