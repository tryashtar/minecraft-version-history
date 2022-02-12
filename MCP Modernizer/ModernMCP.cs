using MinecraftVersionHistory;
using System.IO.Compression;

namespace MCPModernizer;

public class ModernMCP : MCP
{
    public ModernMCP(string mc_version, string tsrg_file, string[] csv_zips)
    {
        ClientVersion = mc_version;

        using (var reader = File.OpenText(tsrg_file))
            MappingsIO.ParseTsrg(LocalMappings.Client, reader);
        using (var reader = File.OpenText(tsrg_file))
            MappingsIO.ParseTsrg(LocalMappings.Server, reader);
        foreach (var csv in csv_zips)
        {
            using var zip = ZipFile.OpenRead(csv);
            StreamReader? read(string path)
            {
                var entry = zip.GetEntry(path);
                if (entry == null)
                    return null;
                return new(entry.Open());
            }
            ParseCSVs(
                classes: read("classes.csv"),
                methods: read("methods.csv"),
                fields: read("fields.csv")
            );
        }
    }
}
