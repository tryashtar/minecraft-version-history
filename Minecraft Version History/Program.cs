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
            {
#endif
                var config = JObject.Parse(File.ReadAllText(@"..\config.json"));
                Console.WriteLine("Java:");
                JavaVersion.ServerJarFolder = (string)config["server_jars"];
                JavaVersion.DecompilerFile = (string)config["decompiler"];
                var java = new JavaUpdater((string)config["java_repo"], (string)config["java_versions"]);
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
