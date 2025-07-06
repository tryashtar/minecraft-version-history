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
            string config_path = Path.Combine(Directory.GetCurrentDirectory(), "config.yaml");
            if (args.Length > 0)
                config_path = Path.Combine(Directory.GetCurrentDirectory(), args[0]);
            var config_file = (YamlMappingNode)YamlHelper.ParseFile(config_path);
            var config = new AppConfig(Path.GetDirectoryName(config_path), config_file);


            var java = new JavaUpdater(config);
#if !DEBUG
            try
#endif
            {
                java.DownloadMissing(config.Java.InputFolders[0].Folder, config);
            }
#if !DEBUG
            catch (Exception ex)
            {
                Console.WriteLine("Java version downloader failed!");
                Console.WriteLine(ex.ToString());
            }
#endif
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