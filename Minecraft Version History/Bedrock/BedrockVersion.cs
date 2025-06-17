namespace MinecraftVersionHistory;

public class BedrockVersion : Version
{
    public readonly string AppxPath;

    public BedrockVersion(string path, VersionFacts facts, bool unzip)
    {
        if (unzip)
        {
            Console.WriteLine($"Extracting APPX for {Path.GetFileName(path)}...");
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                var appx = GetMainAppx(zip);
                AppxPath = Path.ChangeExtension(path, ".appx");
                appx.ExtractToFile(AppxPath);
            }
            File.Delete(path);
        }
        else
            AppxPath = path;

        using ZipArchive zip2 = ZipFile.OpenRead(AppxPath);
        Name = facts.CustomName(Path.GetFileNameWithoutExtension(path));
        if (Name == null)
        {
            var manifest = zip2.GetEntry("AppxManifest.xml");
            using var read = new StreamReader(manifest.Open());
            string data = read.ReadToEnd();
            // too lazy to parse xml
            int version_index = data.IndexOf("Version=\"") + "Version=\"".Length;
            Name = data[version_index..data.IndexOf("\"", version_index)];
        }

        ReleaseTime = zip2.Entries[0].LastWriteTime.UtcDateTime;
    }

    private ZipArchiveEntry GetMainAppx(ZipArchive zip)
    {
        foreach (var entry in zip.Entries)
        {
            string filename = Path.GetFileName(entry.FullName);
            // example: Minecraft.Windows_1.1.0.0_x64_UAP.Release.appx
            if (filename.StartsWith("Minecraft.Windows") && Path.GetExtension(filename) == ".appx")
                return entry;
        }

        throw new FileNotFoundException($"Could not find main APPX");
    }

    public override void ExtractData(string folder, AppConfig config)
    {
        var bedrock_config = config.Bedrock;
        using (ZipArchive zip = ZipFile.OpenRead(AppxPath))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.StartsWith("data/") && Path.GetExtension(entry.FullName) != ".zip")
                {
                    Directory.CreateDirectory(Path.Combine(folder, Path.GetDirectoryName(entry.FullName)));
                    entry.ExtractToFile(Path.Combine(folder, entry.FullName));
                }
            }
        }
        
        foreach (var merger in bedrock_config.PackMergers)
        {
            merger.Merge(folder);
        }
    }
}