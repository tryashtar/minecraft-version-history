using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

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
                    CommandRunner.RunCommand(java_config.ServerJarFolder, $"\"{java_config.JavaInstallationPath}\" -cp \"{ServerJarPath}\" net.minecraft.data.Main --reports");
                    var outputfolder = Path.Combine(folder, "reports");
                    Directory.CreateDirectory(outputfolder);
                    Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), outputfolder);
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
                DoJsonSorting(entry.FullName, destination, java_config);
            }
            Profiler.Stop();
        }

        private void DoJsonSorting(string name, string path, JavaConfig config)
        {
            if (config.NeedsJsonSorting(name))
            {
                var json = JObject.Parse(File.ReadAllText(path));
                config.JsonSort(name, json);
                File.WriteAllText(path, Util.ToMinecraftJson(json));
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
                $"--in-jar \"{jar_path}\" --out-jar \"{mapped_jar_path}\" --srg-in \"{mappings_path}\"");
            Profiler.Stop();
            return mapped_jar_path;
        }

        private void Decompile(JavaConfig config, string jar_path, string mappings_url, string side, string folder)
        {
            string mapped_jar = MapJar(config, jar_path, mappings_url, side, folder);
            jar_path = mapped_jar ?? jar_path;

            Profiler.Start("Removing unwanted files");
            // use the old-style using since the zip needs to dispose before decompiling starts
            using (ZipArchive archive = ZipFile.Open(jar_path, ZipArchiveMode.Update))
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

            Profiler.Start($"Decompiling {side}");
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
            if (mapped_jar is not null)
                File.Delete(mapped_jar);
        }

        private void DecompileMinecraft(JavaConfig config, string destination)
        {
            Directory.CreateDirectory(destination);
            DownloadServerJar(config);
            Decompile(config, ClientJarPath, ClientMappingsURL, "client", destination);
            if (ServerJarPath is not null)
                Decompile(config, ServerJarPath, ServerMappingsURL, "server", destination);
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
            using (var client = new WebClient())
            {
                client.DownloadFile(url, path);
            }
            Profiler.Stop();
        }
    }
}
