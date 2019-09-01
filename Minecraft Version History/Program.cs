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
#if RELEASE
            try
#endif
            {
                Console.WriteLine("Java:");
                JavaVersion.ServerJarFolder = @"D:\~No Sync\mc server jars";
                var java = new JavaUpdater(@"D:\Minecraft\Java Storage\History",
                                           @"D:\~No Sync\Game Directories\.minecraft\versions");
                java.CommitChanges();

                Console.WriteLine("Bedrock:");
                var bedrock = new BedrockUpdater(@"D:\Minecraft\Bedrock Storage\History",
                                                 @"D:\~No Sync\~Unorganized\~mc builds unorganized");
                bedrock.CommitChanges();

                Console.WriteLine("All done!");
            }
#if RELEASE
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }
#endif
        }
    }
}
