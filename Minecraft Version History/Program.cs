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
#if !DEBUG
            start:
            try
#endif
            {
                var config_file = JObject.Parse(File.ReadAllText(@"..\config.json"));
                var java_config = new JavaConfig(Util.PathToObject(config_file, "java"));
                var bedrock_config = new BedrockConfig(Util.PathToObject(config_file, "bedrock"));

                var java = new JavaUpdater2(java_config);
                java.Perform();

                var bedrock = new BedrockUpdater2(bedrock_config);
                bedrock.Perform();

                var version_facts = JObject.Parse(File.ReadAllText(@"..\version_facts.json"));
                var config = JObject.Parse(File.ReadAllText(@"..\config.json"));
                // java 9+ crashes when getting data from some versions (https://bugs.mojang.com/browse/MC-132888)
                JavaVersion.JavaPath = (string)config["java_install"];
                JavaVersion.NbtTranslationJar = (string)config["nbt_translation_jar"];
                JavaVersion.FernflowerJar = (string)config["fernflower_jar"];
                JavaVersion.CfrJar = (string)config["cfr_jar"];
                JavaVersion.SpecialSourceJar = (string)config["special_source_jar"];
                JavaVersion.ServerJarFolder = (string)config["server_jars"];
                JavaVersion.ReleasesMap = (JObject)version_facts["java"]["releases"];
                JavaVersion.Decompiler = JavaVersion.ParseDecompiler((string)config["decompiler"]);
                JavaUpdater.VersionFacts = (JObject)version_facts["java"];
                BedrockUpdater.VersionFacts = (JObject)version_facts["bedrock"];

                Console.WriteLine("Java:");
                var java = new JavaUpdater((string)config["java_repo"], (string)config["java_versions"]);
                java.CommitChanges();

                Console.WriteLine("Bedrock:");
                var bedrock = new BedrockUpdater((string)config["bedrock_repo"], (string)config["bedrock_versions"]);
                bedrock.CommitChanges();

                Console.WriteLine("All done!");
            }
#if !DEBUG
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
