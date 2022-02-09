using System.IO.Compression;

namespace MCPModernizer;

public class ModernMCP : MCP
{
    public readonly string TSRGFile;
    public readonly string CSVZip;

    public ModernMCP(string mc_version, string tsrg_file, string csv_zip)
    {
        ClientVersion = mc_version;
        TSRGFile = tsrg_file;
        CSVZip = csv_zip;

        ParseTSRG(TSRGFile, LocalMappings.Client);
        ParseTSRG(TSRGFile, LocalMappings.Server);
        using var zip = ZipFile.OpenRead(CSVZip);
        StreamReader? read(string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null)
                return null;
            return new(entry.Open());
        }
        ParseCSVs(
            newids: read("newids.csv"),
            classes: read("classes.csv"),
            methods: read("methods.csv"),
            fields: read("fields.csv")
        );
    }

    private void ParseTSRG(string tsrg_file, Mappings mappings)
    {
        using var reader = File.OpenText(tsrg_file);
        string current_class = null;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
            if (line.StartsWith('\t'))
            {
                var entries = line[1..].Split(' ');
                if (entries.Length == 2)
                    mappings.AddField(current_class + "/" + entries[0], entries[1]);
                else if (entries.Length == 3)
                    mappings.AddMethod(current_class + "/" + entries[0], entries[2], entries[1]);
            }
            else
            {
                var entries = line.Split(' ');
                mappings.AddClass(entries[0], entries[1]);
                current_class = entries[0];
            }
        }
    }
}
