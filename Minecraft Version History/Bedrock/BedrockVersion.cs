using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public class BedrockVersion : Version
    {
        public readonly string ZipPath;
        public BedrockVersion(string zip_path)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zip_path))
            {
                ZipPath = zip_path;
                var mainappx = GetMainAppx(zip);
                Name = Path.GetFileName(mainappx.FullName).Split('_')[1];
                ReleaseTime = zip.Entries[0].LastWriteTime.UtcDateTime;
            }
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
            string appxpath = Path.Combine(folder, "appx.appx");
            using (ZipArchive zip = ZipFile.OpenRead(ZipPath))
            {
                var appx = GetMainAppx(zip);
                appx.ExtractToFile(appxpath);
            }

            using (ZipArchive zip = ZipFile.OpenRead(appxpath))
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
            var merged = Path.Combine(folder, "latest_packs");
            var latest_behavior = Path.Combine(merged, "behavior_pack");
            var latest_resource = Path.Combine(merged, "resource_pack");
            Directory.CreateDirectory(merged);
            Directory.CreateDirectory(latest_behavior);
            Directory.CreateDirectory(latest_resource);
            bedrock_config.BehaviorMerger.Merge(Path.Combine(folder, "data", "behavior_packs"), latest_behavior);
            bedrock_config.ResourceMerger.Merge(Path.Combine(folder, "data", "resource_packs"), latest_resource);
            File.Delete(appxpath);
        }
    }
}
