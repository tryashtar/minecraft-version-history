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
using YamlDotNet.RepresentationModel;

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
                var config_file = Util.ParseYamlFile(@"..\config.yaml");
                var java_config = new JavaConfig(Path.GetFullPath(".."), config_file["java"] as YamlMappingNode);
                var bedrock_config = new BedrockConfig(config_file["bedrock"] as YamlMappingNode);

                var java = new JavaUpdater(java_config);
                java.Perform();

                //var bedrock = new BedrockUpdater(bedrock_config);
                //bedrock.Perform();

                Console.WriteLine("All done!");
#if DEBUG
                Console.ReadLine();
#endif
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
