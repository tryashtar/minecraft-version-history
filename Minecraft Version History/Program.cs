using fNbt;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    class Program
    {
        static void Main(string[] args)
        {
            string source = Path.Combine(Directory.GetCurrentDirectory(), "nbt");
            string dest = Path.Combine(Directory.GetCurrentDirectory(), "snbt");
            if (Directory.Exists(source))
            {
                Directory.CreateDirectory(dest);
                foreach (var structure in Directory.EnumerateFiles(source, "*.mcstructure", SearchOption.TopDirectoryOnly))
                {
                    var file = new NbtFile();
                    file.BigEndian = false;
                    file.LoadFromFile(structure);
                    File.WriteAllText(Path.Combine(dest, Path.ChangeExtension(Path.GetFileName(structure), ".snbt")), file.RootTag.ToSnbt(true) + "\n");
                }
            }


#if !DEBUG
            start:
            try
            {
#endif
            var version_facts = JObject.Parse(File.ReadAllText(@"..\version_facts.json"));
            var config = JObject.Parse(File.ReadAllText(@"..\config.json"));
            // java 9+ crashes when getting data from some versions (https://bugs.mojang.com/browse/MC-132888)
            JavaVersion.JavaPath = (string)config["java_install"];
            JavaVersion.NbtTranslationJar = (string)config["nbt_translation_jar"];
            JavaVersion.ServerJarFolder = (string)config["server_jars"];
            JavaVersion.DecompilerFile = (string)config["decompiler"];
            JavaVersion.ReleasesMap = (JObject)version_facts["releases"];

            Console.WriteLine("Java:");
            var java = new JavaUpdater(version_facts, (string)config["java_repo"], (string)config["java_versions"]);
            java.CommitChanges();

            Console.WriteLine("Bedrock:");
            var bedrock = new BedrockUpdater((string)config["bedrock_repo"], (string)config["bedrock_versions"]);
            bedrock.CommitChanges();

            Console.WriteLine("All done!");
#if !DEBUG
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
                Console.WriteLine("Press enter to try again");
                Console.ReadLine();
                goto start;
            }
#endif
        }
    }
}
