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
        public readonly string JarPath;
        public readonly string ServerJarURL;
        public readonly string MappingsURL;
        public JavaVersion(string folder)
        {
            Name = Path.GetFileName(folder);
            string jsonpath = Path.Combine(folder, Name + ".json");
            JarPath = Path.Combine(folder, Name + ".jar");
            var json = JObject.Parse(File.ReadAllText(jsonpath));
            ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            ServerJarURL = (string)json["downloads"]?["server"]?["url"];
            MappingsURL = (string)json["downloads"]?["client_mappings"]?["url"];
        }

        public override void ExtractData(string folder, Config config)
        {
            var java_config = (JavaConfig)config;
            if (ReleaseTime > java_config.DataGenerators)
            {
                Console.WriteLine("Fetching data reports...");
                string reports_path = Path.Combine(java_config.ServerJarFolder, "generated");
                if (Directory.Exists(reports_path))
                    Directory.Delete(reports_path, true);

                var serverjar = Path.Combine(java_config.ServerJarFolder, Name + ".jar");
                if (!File.Exists(serverjar))
                    DownloadServerJar(serverjar);
                CommandRunner.RunCommand(java_config.ServerJarFolder, $"\"{java_config.JavaInstallationPath}\" -cp \"{serverjar}\" net.minecraft.data.Main --reports");
                var outputfolder = Path.Combine(folder, "reports");
                Directory.CreateDirectory(outputfolder);

                Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), outputfolder);
            }
            DecompileMinecraft(java_config, Path.Combine(folder, "source"));

            Console.WriteLine($"Extracting jar... ({this})");
            using (ZipArchive zip = ZipFile.OpenRead(JarPath))
            {
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

        private void DecompileMinecraft(JavaConfig config, string destination)
        {
            Directory.CreateDirectory(destination);
            string jar_path = JarPath;
            Action cleanup = null;
            if (MappingsURL != null)
            {
                string mappings_path = Path.Combine(Path.GetDirectoryName(destination), "mappings.txt");
                string tsrg_path = Path.Combine(destination, "tsrg.tsrg");
                string mapped_jar_path = Path.Combine(destination, "mapped.jar");
                DownloadMappings(mappings_path);
                ConvertMappings(mappings_path, tsrg_path);
                RemapJar(config, tsrg_path, mapped_jar_path);
                jar_path = mapped_jar_path;
                cleanup = () =>
                {
                    File.Delete(tsrg_path);
                    File.Delete(mapped_jar_path);
                };
            }

            if (config.Decompiler == DecompilerType.Cfr)
            {
                Console.WriteLine($"Decompiling with CFR...");
                var result = CommandRunner.RunCommand(destination, $"\"{config.JavaInstallationPath}\" -Xmx{config.DecompilerXmx} -Xms{config.DecompilerXmx} -jar \"{config.CfrPath}\" \"{jar_path}\" " +
                    $"--outputdir {destination} --caseinsensitivefs true --comments false --showversion false");
                if (result.ExitCode != 0)
                    throw new ApplicationException("Failed to decompile");
                string summary_file = Path.Combine(destination, "summary.txt");
                if (File.Exists(summary_file))
                {
                    Console.WriteLine("Summary:");
                    Console.WriteLine(File.ReadAllText(summary_file));
                    cleanup += () => File.Delete(summary_file);
                }
            }
            else if (config.Decompiler == DecompilerType.Fernflower)
            {
                Console.WriteLine($"Decompiling with fernflower...");
                string output_dir = Path.Combine(destination, "decompiled");
                Directory.CreateDirectory(output_dir);
                CommandRunner.RunCommand(destination, $"\"{config.JavaInstallationPath}\" -Xmx1200M -Xms200M -jar \"{config.FernflowerPath}\" " +
                    $"-hes=0 -hdc=0 -dgs=1 -log=WARN \"{jar_path}\" \"{output_dir}\""); ;
                using (ZipArchive zip = ZipFile.OpenRead(Path.Combine(output_dir, Path.GetFileName(jar_path))))
                {
                    foreach (var entry in zip.Entries)
                    {
                        Directory.CreateDirectory(Path.Combine(destination, Path.GetDirectoryName(entry.FullName)));
                        entry.ExtractToFile(Path.Combine(destination, entry.FullName));
                    }
                }
                cleanup += () => Directory.Delete(output_dir, true);
            }
            else
                throw new ArgumentException(nameof(config.Decompiler));

            cleanup?.Invoke();
        }

        private void DownloadMappings(string path) => DownloadThing(MappingsURL, path, "mappings");
        private void DownloadServerJar(string path) => DownloadThing(ServerJarURL, path, "server jar");

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

        private void RemapJar(JavaConfig config, string tsrg_path, string output_path)
        {
            Console.WriteLine("Remapping jar...");
            CommandRunner.RunCommand(Path.GetDirectoryName(output_path), $"\"{config.JavaInstallationPath}\" -jar \"{config.SpecialSourcePath}\" " +
                $"--in-jar \"{JarPath}\" --out-jar \"{output_path}\" --srg-in \"{tsrg_path}\" --kill-lvt");
        }
    }
}
