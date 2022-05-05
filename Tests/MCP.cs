using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MinecraftVersionHistory;
using Newtonsoft.Json.Linq;
using TryashtarUtils.Utility;
using YamlDotNet.RepresentationModel;

namespace Tests;

[TestClass]
public class MCP
{
    [TestMethod]
    public void TestAllMCP()
    {
        //var path = @"..\..\..\..\Minecraft Version History\bin\config.yaml";
        //var config_file = (YamlMappingNode)YamlHelper.ParseFile(path);
        //var config = new AppConfig(Path.GetDirectoryName(Path.GetFullPath(path)), config_file);
        //var java = new JavaUpdater(config);
        //foreach (var node in java.Graph.Flatten())
        //{
        //    var version = (JavaVersion)node.Version;
        //    var mcp = config.Java.GetBestMCP(version);
        //    if (mcp != null)
        //    {
        //        //Console.WriteLine($"Testing {version.Name} with MCP {mcp.Version}");
        //        //bool success = true;
        //        //try
        //        //{
        //        mcp.CreateClientMappings("test.txt");
        //        config.Java.RemapJar(version.Client.JarPath, "test.txt", "test.jar");
        //        if (version.Server.JarPath == null)
        //            version.DownloadServerJar(config.Java);
        //        if (version.Server.JarPath != null)
        //        {
        //            mcp.CreateServerMappings("test.txt");
        //            config.Java.RemapJar(version.Server.JarPath, "test.txt", "test.jar");
        //        }
        //        //}
        //        //catch (Exception ex)
        //        //{
        //        //    success = false;
        //        //    Console.ForegroundColor = ConsoleColor.Red;
        //        //    Console.WriteLine($"\tFAILED! {ex}");
        //        //    Console.ResetColor();
        //        //}
        //        //if (success)
        //        //{
        //        //    Console.ForegroundColor = ConsoleColor.Green;
        //        //    Console.WriteLine($"\tPassed!");
        //        //    Console.ResetColor();
        //        //}
        //    }
        //}
    }
}
