using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class JavaVersionOLD
    {
        private readonly string JarPath;
        private readonly string ServerJarURL;
        private readonly string MappingsURL;

        private static readonly Regex SnapshotRegex = new Regex(@"(\d\d)w(\d\d)[a-z~]");
        private static readonly DateTime DataGenerators = new DateTime(2018, 1, 1);
        private static readonly DateTime AssetGenerators = new DateTime(2020, 3, 1);
        private static readonly string[] ModelOrder = new string[] { "model", "x", "y", "z", "uvlock", "weight" };
        private static readonly string[] IllegalNames = new[] { "aux", "con", "clock$", "nul", "prn", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
        public JavaVersionOLD(string folder)
        {
            //Name = Path.GetFileName(folder);
            //string jsonpath = Path.Combine(folder, Name + ".json");
            //JarPath = Path.Combine(folder, Name + ".jar");
            //var json = JObject.Parse(File.ReadAllText(jsonpath));
            //ReleaseTime = DateTime.Parse((string)json["releaseTime"]);
            //ServerJarURL = (string)json["downloads"]?["server"]?["url"];
            //MappingsURL = (string)json["downloads"]?["client_mappings"]?["url"];
        }

        public void ExtractData(string output)
        {
            //if (ReleaseTime > DataGenerators)
            //{
            //    Console.WriteLine("Fetching data reports...");
            //    string reports_path = Path.Combine(ServerJarFolder, "generated");
            //    if (Directory.Exists(reports_path))
            //        Directory.Delete(reports_path, true);
            //
            //    var serverjar = Path.Combine(ServerJarFolder, Name + ".jar");
            //    if (!File.Exists(serverjar))
            //        DownloadServerJar(serverjar);
            //    var run = CommandRunner.RunCommand(ServerJarFolder, $"\"{JavaVersionOLD.JavaPath}\" -cp \"{serverjar}\" net.minecraft.data.Main --reports");
            //    var outputfolder = Path.Combine(output, "reports");
            //    Directory.CreateDirectory(outputfolder);
            //
            //    Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(Path.Combine(reports_path, "reports"), outputfolder);
            //}
            //DecompileMinecraft(Decompiler, Path.Combine(output, "source"));

            Console.WriteLine("Extracting jar...");
            using (ZipArchive zip = ZipFile.OpenRead(JarPath))
            {
                foreach (var entry in zip.Entries)
                {
                    string filename = Path.GetFileName(entry.FullName);
                    if (entry.FullName.EndsWith("/") || IllegalNames.Contains(filename.ToLower()) || filename.EndsWith(".class") || filename.EndsWith(".xml") || entry.FullName.Contains("META-INF"))
                        continue;
                    Directory.CreateDirectory(Path.Combine(output, "jar", Path.GetDirectoryName(entry.FullName)));
                    var destination = Path.Combine(output, "jar", entry.FullName);
                    entry.ExtractToFile(destination);
                    // sort special files that are arbitrarily ordered with each extraction
                    if (entry.FullName == "data/minecraft/advancements/nether/all_effects.json" ||
                        entry.FullName == "data/minecraft/advancements/nether/all_potions.json")
                    {
                        var advancement = JObject.Parse(File.ReadAllText(destination));
                        Util.SortKeys((JObject)advancement["criteria"]["all_effects"]["conditions"]["effects"]);
                        File.WriteAllText(destination, Util.ToMinecraftJson(advancement));
                    }
                    else if (entry.FullName == "data/minecraft/loot_tables/chests/shipwreck_supply.json")
                    {
                        var table = JObject.Parse(File.ReadAllText(destination));
                        var stew = ((JArray)table["pools"][0]["entries"]).FirstOrDefault(x => x["name"].ToString() == "minecraft:suspicious_stew");
                        if (stew != null)
                        {
                            var function = (JObject)stew["functions"][0];
                            function["effects"] = new JArray(((JArray)function["effects"]).OrderBy(x => x["type"].ToString()));
                            File.WriteAllText(destination, Util.ToMinecraftJson(table));
                        }
                    }
                    //else if (ReleaseTime > AssetGenerators && Path.GetDirectoryName(entry.FullName) == @"assets\minecraft\blockstates")
                    {
                        var blockstate = JObject.Parse(File.ReadAllText(destination));
                        if (blockstate.TryGetValue("variants", out var variants))
                        {
                            foreach (var variant in (JObject)variants)
                            {
                                if (variant.Value is JArray many)
                                {
                                    foreach (JObject option in many)
                                    {
                                        Util.SortKeys(option, ModelOrder);
                                    }
                                }
                                else
                                    Util.SortKeys((JObject)variant.Value, ModelOrder);
                            }
                        }
                        else if (blockstate.TryGetValue("multipart", out var multipart))
                        {
                            foreach (JObject part in (JArray)multipart)
                            {
                                var apply = part["apply"];
                                if (apply is JArray many)
                                {
                                    foreach (JObject item in many)
                                    {
                                        Util.SortKeys(item, ModelOrder);
                                    }
                                }
                                else
                                    Util.SortKeys((JObject)apply, ModelOrder);
                                if (part.TryGetValue("when", out var when))
                                {
                                    if (((JObject)when).TryGetValue("OR", out var or))
                                    {
                                        foreach (JObject option in or)
                                        {
                                            Util.SortKeys(option);
                                        }
                                    }
                                    else
                                        Util.SortKeys((JObject)when);
                                }
                            }
                        }
                        File.WriteAllText(destination, Util.ToMinecraftJson(blockstate));
                    }
                }
            }
        }

        public enum DecompilerChoice
        {
            Fernflower,
            Cfr
        }
        public static DecompilerChoice ParseDecompiler(string input)
        {
            if (String.Equals(input, "fernflower", StringComparison.OrdinalIgnoreCase))
                return DecompilerChoice.Fernflower;
            if (String.Equals(input, "cfr", StringComparison.OrdinalIgnoreCase))
                return DecompilerChoice.Cfr;
            throw new ArgumentException(input);
        }
        private void DecompileMinecraft(DecompilerChoice decompiler, string destination)
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
                RemapJar(tsrg_path, mapped_jar_path);
                jar_path = mapped_jar_path;
                cleanup = () =>
                {
                    File.Delete(tsrg_path);
                    File.Delete(mapped_jar_path);
                };
            }

            if (decompiler == DecompilerChoice.Cfr)
            {
                Console.WriteLine($"Decompiling with CFR...");
                //CommandRunner.RunCommand(destination, $"\"{JavaVersionOLD.JavaPath}\" -Xmx1200M -Xms200M -jar \"{JavaVersionOLD.CfrJar}\" \"{jar_path}\" " +
                //    $"--outputdir {destination} --caseinsensitivefs true --comments false --showversion false");
                string summary_file = Path.Combine(destination, "summary.txt");
                if (File.Exists(summary_file))
                {
                    Console.WriteLine("Summary:");
                    Console.WriteLine(File.ReadAllText(summary_file));
                    cleanup += () => File.Delete(summary_file);
                }
            }
            else if (decompiler == DecompilerChoice.Fernflower)
            {
                Console.WriteLine($"Decompiling with fernflower...");
                string output_dir = Path.Combine(destination, "decompiled");
                Directory.CreateDirectory(output_dir);
                //CommandRunner.RunCommand(destination, $"\"{JavaVersionOLD.JavaPath}\" -Xmx1200M -Xms200M -jar \"{JavaVersionOLD.FernflowerJar}\" " +
                //    $"-hes=0 -hdc=0 -dgs=1 -log=WARN \"{jar_path}\" \"{output_dir}\""); ;
                using (ZipArchive zip = ZipFile.OpenRead(Path.Combine(output_dir, Path.GetFileName(jar_path))))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.StartsWith("com") || entry.FullName.StartsWith("net"))
                        {
                            Directory.CreateDirectory(Path.Combine(destination, Path.GetDirectoryName(entry.FullName)));
                            entry.ExtractToFile(Path.Combine(destination, entry.FullName));
                        }
                    }
                }
                cleanup += () => Directory.Delete(output_dir, true);
            }
            else
                throw new ArgumentException(nameof(decompiler));

            cleanup?.Invoke();
        }

        private void DownloadMappings(string path)
        {
            Console.WriteLine("Downloading mappings...");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var client = new WebClient())
            {
                client.DownloadFile(MappingsURL, path);
            }
            Console.WriteLine("Download complete!");
        }

        private void DownloadServerJar(string path)
        {
            Console.WriteLine("Downloading server jar...");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var client = new WebClient())
            {
                client.DownloadFile(ServerJarURL, path);
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

        private void RemapJar(string tsrg_path, string output_path)
        {
            Console.WriteLine("Remapping jar...");
            //CommandRunner.RunCommand(Path.GetDirectoryName(output_path), $"\"{JavaVersionOLD.JavaPath}\" -jar \"{JavaVersionOLD.SpecialSourceJar}\" " +
            //    $"--in-jar \"{JarPath}\" --out-jar \"{output_path}\" --srg-in \"{tsrg_path}\" --kill-lvt");
        }

        // facts of versions
        private string GetMadeForRelease(string versionname)
        {
            // possible formats:
            // 1.x.x        1.x
            // a1.x.x       Alpha 1.x
            // b1.x.x       Beta 1.x
            // c1.x.x       Alpha 1.x
            // inf-xxxx     Infdev
            // rd-xxxx      Classic
            // yywxxl       (needs lookup)

            //if ((ReleasesMap["special"] as JObject).TryGetValue(versionname, out var release))
            //    return (string)release;

            // real versions
            if (versionname.StartsWith("1."))
                return MajorMinor(versionname);
            if (versionname.StartsWith("a1."))
                return "Alpha " + MajorMinor(versionname.Substring(1));
            if (versionname.StartsWith("b1."))
                return "Beta " + MajorMinor(versionname.Substring(1));
            if (versionname.StartsWith("inf-"))
                return "Infdev";
            if (versionname.StartsWith("in-"))
                return "Indev";
            if (versionname.StartsWith("c"))
                return "Classic";
            if (versionname.StartsWith("rd-"))
                return "Pre-Classic";

            var match = SnapshotRegex.Match(versionname);
            if (match.Success)
            {
                int year = int.Parse(match.Groups[1].Value);
                int week = int.Parse(match.Groups[2].Value);

                //foreach (var snapshot in (JObject)ReleasesMap["snapshots"])
                //{
                //    string[] parts = snapshot.Key.Split('.');
                //    int template_year = int.Parse(parts[0]);
                //    int template_week = int.Parse(parts[1]);
                //    if (year == template_year && week <= template_week)
                //        return (string)snapshot.Value;
                //}
            }
            throw new ArgumentException($"Could not determine the version to which {versionname} belongs");
        }

        private static string MajorMinor(string versionname)
        {
            if (versionname.Count(x => x == '.') < 2)
            {
                var ends = new[] { '-', ' ', '_', 'a', 'b', 'c', 'd' };
                int bestresult = int.MaxValue;
                string final = versionname;
                foreach (var end in ends)
                {
                    int index = versionname.IndexOf(end);
                    if (index != -1 && index < bestresult)
                    {
                        bestresult = index;
                        final = versionname.Substring(0, index);
                    }
                }
                return final;
            }
            return versionname.Substring(0, versionname.IndexOf('.', versionname.IndexOf('.') + 1));
        }
    }
}
