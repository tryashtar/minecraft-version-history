namespace MinecraftVersionHistory;

public static class Program
{
    public static void Main(string[] args)
    {
#if !DEBUG
            start:
            try
#endif
        {
            var config_file = (YamlMappingNode)YamlHelper.ParseFile(@"../../config.yaml");
            var config = new AppConfig(Path.GetFullPath(@"../.."), config_file);

            var downloader = new JavaVersionDownloader();
#if !DEBUG
                try
#endif
            {
                downloader.DownloadMissing(config.Java.InputFolders, config);
            }
#if !DEBUG
                catch (Exception ex)
                {
                    Console.WriteLine("Java version downloader failed!");
                    Console.WriteLine(ex.ToString());
                }
#endif

            var java = new JavaUpdater(config);
            java.Perform();

            var bedrock = new BedrockUpdater(config);
            bedrock.Perform();

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
