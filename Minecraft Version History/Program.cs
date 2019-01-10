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
            JavaVersion.ServerJarFolder = @"D:\Projects\Visual Studio\Java Updater\Java Updater\bin\Debug\settings\servers";
            var java = new JavaUpdater(//@"D:\Minecraft\Java Storage\Java History",
                @"D:\~No Sync\tests\java",
                                       Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft\versions"));

            java.CommitChanges();

            var bedrock = new BedrockUpdater(//@"D:\Minecraft\Bedrock Storage\Bedrock History",
                @"D:\~No Sync\tests\bedrock",
                                             @"D:\~No Sync\~Unorganized\~mc builds unorganized");

            bedrock.CommitChanges();

            Console.ReadLine();
        }
    }
}
