using MinecraftVersionHistory;
using System.IO.Compression;

namespace MCPModernizer;

public class ModernMCP : MCP
{
    public readonly string TsrgFile;
    public readonly string CsvZip;

    public ModernMCP(string mc_version, string tsrg_file, string csv_zip)
    {
        ClientVersion = mc_version;
        TsrgFile = tsrg_file;
        CsvZip = csv_zip;

        using var reader = File.OpenText(TsrgFile);
        MappingsIO.ParseTsrg(LocalMappings.Client, reader);
        MappingsIO.ParseTsrg(LocalMappings.Server, reader);
        using var zip = ZipFile.OpenRead(CsvZip);
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
