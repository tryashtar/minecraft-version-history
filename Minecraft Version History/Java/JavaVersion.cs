using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using System.Net.Http;

namespace MinecraftVersionHistory
{
    public class JavaVersion : Version
    {
        public readonly string ClientJarPath;
        public string ServerJarPath { get; private set; }
        public readonly string ServerJarURL;
        public readonly string ClientMappingsURL;
        public readonly string ServerMappingsURL;
        public JavaVersion(string folder)
        {
            Name = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, Name + ".json");
            ClientJarPath = Path.Combine(folder, Name + ".jar");
            var json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            ServerJarURL = (string)json["downloads"]?["server"]?["url"];
            ClientMappingsURL = (string)json["downloads"]?["client_mappings"]?["url"];
            ServerMappingsURL = (string)json["downloads"]?["server_mappings"]?["url"];
        }

        public static bool LooksValid(string folder)
        {
            string name = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, name + ".json");
            string jarpath = Path.Combine(folder, name + ".jar");
            return File.Exists(jsonpath) && File.Exists(jarpath);
        }

        public override void ExtractData(string folder, AppConfig config)
        {
            var java_config = config.Java;
            if (ReleaseTime > java_config.DataGenerators)
            {
                string reports_path = Path.Combine(java_config.ServerJarFolder, "generated");
                if (Directory.Exists(reports_path))
                    Directory.Delete(reports_path, true);

                DownloadServerJar(java_config);
                if (ServerJarPath is not null)
                {
                    Profiler.Start("Fetching data reports");
                    var result = CommandRunner.RunCommand(java_config.ServerJarFolder, $"\"{java_config.JavaInstallationPath}\" -cp \"{ServerJarPath}\" net.minecraft.data.Main --reports");
                    if (result.ExitCode != 0)
                        result = CommandRunner.RunCommand(java_config.ServerJarFolder, $"\"{java_config.JavaInstallationPath}\" -DbundlerMainClass=net.minecraft.data.Main -jar \"{ServerJarPath}\" --reports");
                    if (result.ExitCode != 0)
                        throw new ApplicationException("Failed to get data reports");
                    var outputfolder = Path.Combine(folder, "reports");
                    Directory.CreateDirectory(outputfolder);
                    FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), outputfolder);
                    if (Directory.Exists(reports_path))
                        Directory.Delete(reports_path, true);
                    Profiler.Stop();
                }
            }
            DecompileMinecraft(java_config, Path.Combine(folder, "source"));

            Profiler.Start($"Extracting jar");
            using ZipArchive zip = ZipFile.OpenRead(ClientJarPath);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith("/") || java_config.ExcludeJarEntry(entry.FullName))
                    continue;
                Directory.CreateDirectory(Path.Combine(folder, "jar", Path.GetDirectoryName(entry.FullName)));
                var destination = Path.Combine(folder, "jar", entry.FullName);
                entry.ExtractToFile(destination);
            }
            DoJsonSorting(folder, java_config);
            Profiler.Stop();
        }

        private void DoJsonSorting(string folder, JavaConfig config)
        {
            foreach (var file in config.NeedsJsonSorting())
            {
                var path = Path.Combine(folder, file);
                if (File.Exists(path))
                {
                    var json = JObject.Parse(File.ReadAllText(path));
                    config.JsonSort(file, json);
                    File.WriteAllText(path, Util.ToMinecraftJson(json));
                }
            }
        }

        private string MapJar(JavaConfig config, string jar_path, string mappings_url, string side, string folder)
        {
            if (mappings_url == null)
                return null;
            string mappings_path = Path.Combine(Path.GetDirectoryName(folder), $"mappings_{side}.txt");
            string mapped_jar_path = Path.Combine(folder, $"mapped_{side}.jar");
            DownloadThing(mappings_url, mappings_path, $"{side} mappings");
            Profiler.Start("Remapping jar with SpecialSource");
            CommandRunner.RunCommand(Path.GetDirectoryName(mapped_jar_path), $"\"{config.JavaInstallationPath}\" -jar \"{config.SpecialSourcePath}\" " +
                $"--in-jar \"{jar_path}\" --out-jar \"{mapped_jar_path}\" --srg-in \"{mappings_path}\" --kill-lvt");
            Profiler.Stop();
            return mapped_jar_path;
        }

        private void DecompileJar(JavaConfig config, string jar_path, string folder)
        {
            Profiler.Start($"Decompiling");
            if (config.Decompiler == DecompilerType.Cfr)
            {
                var result = CommandRunner.RunCommand(folder, $"\"{config.JavaInstallationPath}\" {config.DecompilerArgs} -jar \"{config.CfrPath}\" \"{jar_path}\" " +
                    $"--outputdir \"{folder}\" {config.CfrArgs}");
                if (result.ExitCode != 0)
                    throw new ApplicationException("Failed to decompile");
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
                CommandRunner.RunCommand(folder, $"\"{config.JavaInstallationPath}\" {config.DecompilerArgs} -jar \"{config.FernflowerPath}\" " +
                    $"{config.FernflowerArgs} \"{jar_path}\" \"{output_dir}\""); ;
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
            string final_jar = Path.Combine(destination, $"{Path.GetFileNameWithoutExtension(ClientJarPath)}_final.jar");
            string mapped_client = MapJar(config, ClientJarPath, ClientMappingsURL, "client", destination);
            string used_client = mapped_client ?? ClientJarPath;
            string mapped_server = null;
            string unbundled_server_path = null;
            DownloadServerJar(config);
            string final_server_jar = ServerJarPath;
            if (ServerJarPath is not null)
            {
                using (ZipArchive archive = ZipFile.Open(ServerJarPath, ZipArchiveMode.Read))
                {
                    var libraries = ReadClassPath(archive);
                    if (libraries != null)
                    {
                        Profiler.Start("Unbundling server");

                        unbundled_server_path = Path.Combine(destination, $"{Path.GetFileNameWithoutExtension(ServerJarPath)}_unbundled.jar");
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
                mapped_server = MapJar(config, final_server_jar, ServerMappingsURL, "server", destination);
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

        private void DownloadServerJar(JavaConfig config)
        {
            if (ServerJarURL is null)
                return;
            string path = Path.Combine(config.ServerJarFolder, Name + ".jar");
            if (ServerJarPath is null)
                ServerJarPath = path;
            if (!File.Exists(path))
            {
                DownloadThing(ServerJarURL, path, "server jar");
                ServerJarPath = path;
            }
        }

        private static void DownloadThing(string url, string path, string thing)
        {
            Profiler.Start($"Downloading {thing}");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var client = new HttpClient();
            using var stream = client.GetStreamAsync(url).Result;
            using var file = File.Create(path);
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(file);
            Profiler.Stop();
        }
    }
}
