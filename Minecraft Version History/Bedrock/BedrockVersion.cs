using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
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
                VersionName = Path.GetFileName(mainappx.FullName).Split('_')[1];
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
    }
}
