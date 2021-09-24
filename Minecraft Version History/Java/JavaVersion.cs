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
                Console.WriteLine("Fetching data reports...");
                string reports_path = Path.Combine(java_config.ServerJarFolder, "generated");
                if (Directory.Exists(reports_path))
                    Directory.Delete(reports_path, true);

                DownloadServerJar(java_config);
                CommandRunner.RunCommand(java_config.ServerJarFolder, $"\"{java_config.JavaInstallationPath}\" -cp \"{ServerJarPath}\" net.minecraft.data.Main --reports");
                var outputfolder = Path.Combine(folder, "reports");
                Directory.CreateDirectory(outputfolder);

                Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), outputfolder);
            }
            DecompileMinecraft(java_config, Path.Combine(folder, "source"));

            Console.WriteLine($"Extracting jar... ({this})");
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
            string tsrg_path = Path.Combine(folder, $"tsrg_{side}.tsrg");
            string mapped_jar_path = Path.Combine(folder, $"mapped_{side}.jar");
            DownloadThing(mappings_url, mappings_path, $"{side} mappings");
            ConvertMappings(mappings_path, tsrg_path);
            RemapJar(config, jar_path, tsrg_path, mapped_jar_path);
            File.Delete(tsrg_path);
            return mapped_jar_path;
        }

        private void CombineJars(string destination, params string[] paths)
        {
            Console.WriteLine($"Combining {paths.Length} jar files...");
            using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
            foreach (var path in paths)
            {
                using ZipArchive zip = ZipFile.OpenRead(path);
                foreach (var entry in zip.Entries)
                {
                    var file = archive.CreateEntry(entry.FullName);
                    using var stream = file.Open();
                    using var writer = new StreamWriter(stream);
                    writer.Write(entry.Open());
                }
            }
        }

        private void Decompile(JavaConfig config, string jar_path, string mappings_url, string side, string folder)
        {
            string mapped_jar = MapJar(config, jar_path, mappings_url, side, folder);
            jar_path = mapped_jar ?? jar_path;

            if (config.Decompiler == DecompilerType.Cfr)
            {
                Console.WriteLine($"Decompiling {side} with CFR...");
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
                Console.WriteLine($"Decompiling {side} with fernflower...");
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

            Console.WriteLine("Removing unwanted files...");
            if (mapped_jar is not null)
                File.Delete(mapped_jar);
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var relative_path = Util.RelativePath(folder, file).Replace('\\', '/');
                if (config.ExcludeDecompiledEntry(relative_path))
                    File.Delete(file);
            }
        }

        private void DecompileMinecraft(JavaConfig config, string destination)
        {
            Directory.CreateDirectory(destination);
            DownloadServerJar(config);
            Decompile(config, ClientJarPath, ClientMappingsURL, "client", destination);
            Decompile(config, ServerJarPath, ServerMappingsURL, "server", destination);
        }

        private void DownloadServerJar(JavaConfig config)
        {
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
            Console.WriteLine($"Downloading {thing}...");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var client = new WebClient())
            {
                client.DownloadFile(url, path);
            }
            Console.WriteLine("Download complete!");
        }

        private static readonly Dictionary<string, string> PrimitiveMappings = new Dictionary<string, string>
        {
            { "int", "I" },
            { "double", "D" },
            { "boolean", "Z" },
            { "float", "F" },
            { "long", "J" },
            { "byte", "B" },
            { "short", "S" },
            { "char", "C" },
            { "void", "V" }
        };
        private static string RemapIdentifier(string identifier)
        {
            if (PrimitiveMappings.TryGetValue(identifier, out string result))
                return result;
            return "L" + String.Join("/", identifier.Split('.')) + ";";
        }
        private string ConvertMapping(string mapping, Dictionary<string, string> obfuscation_map)
        {
            int array_length_type = Regex.Matches(mapping, Regex.Escape("[]")).Count;
            mapping = mapping.Replace("[]", "");
            mapping = RemapIdentifier(mapping);
            if (obfuscation_map.TryGetValue(mapping, out var mapped))
                mapping = "L" + mapped + ";";
            mapping = mapping.Replace('.', '/');
            mapping = new string('[', array_length_type) + mapping;
            return mapping;
        }
        private void ConvertMappings(string mappings_path, string output_path)
        {
            Console.WriteLine("Converting mappings to TSRG...");
            var lines = File.ReadAllLines(mappings_path).Where(x => !x.StartsWith("#"));
            var output = new List<string>();
            var class_maps = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("    "))
                    continue;
                string[] names = line.Split(new[] { " -> " }, StringSplitOptions.None);
                string obfuscated_name = names[1].Split(':')[0];
                string deobfuscated_name = names[0];
                class_maps[RemapIdentifier(deobfuscated_name)] = obfuscated_name;
            }
            foreach (var line in lines)
            {
                string[] names = line.Split(new[] { " -> " }, StringSplitOptions.None);
                if (line.StartsWith("    "))
                {
                    string obfuscated_name = names[1].TrimEnd();
                    string deobfuscated_name = names[0].TrimStart();
                    string[] type_name = deobfuscated_name.Split(' ');
                    string method_name = type_name[1];
                    string method_type = type_name[0].Split(':').Last();
                    if (method_name.Contains("(") && method_name.Contains(")"))
                    {
                        string variables = method_name.Split('(').Last().Split(')')[0];
                        string function_name = method_name.Split('(')[0];
                        method_type = ConvertMapping(method_type, class_maps);
                        if (variables != "")
                        {
                            string[] variable_list = variables.Split(',');
                            variable_list = variable_list.Select(x => ConvertMapping(x, class_maps)).ToArray();
                            variables = String.Join("", variable_list);
                        }
                        output.Add($"\t{obfuscated_name} ({variables}){method_type} {function_name}");
                    }
                    else
                        output.Add($"\t{obfuscated_name} {method_name}");
                }
                else
                {
                    string obfuscated_name = names[1].Split(':')[0];
                    string deobfuscated_name = names[0];
                    obfuscated_name = RemapIdentifier(obfuscated_name);
                    deobfuscated_name = RemapIdentifier(deobfuscated_name);
                    output.Add($"{obfuscated_name.Substring(1, obfuscated_name.Length - 2)} {deobfuscated_name.Substring(1, deobfuscated_name.Length - 2)}");
                }
            }
            File.WriteAllLines(output_path, output);
        }

        private void RemapJar(JavaConfig config, string jar_path, string tsrg_path, string output_path)
        {
            Console.WriteLine("Remapping jar...");
            CommandRunner.RunCommand(Path.GetDirectoryName(output_path), $"\"{config.JavaInstallationPath}\" -jar \"{config.SpecialSourcePath}\" " +
                $"--in-jar \"{jar_path}\" --out-jar \"{output_path}\" --srg-in \"{tsrg_path}\" --kill-lvt");
        }
    }
}
