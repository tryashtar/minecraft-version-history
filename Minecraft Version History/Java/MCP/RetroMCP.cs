namespace MinecraftVersionHistory;

public class RetroMCP
{
    public readonly string Folder;
    public readonly Sided<FriendlyNames> MojangMatched = new();
    public RetroMCP(string folder, string matched_version)
    {
        Folder = folder;
        var mojang = new Sided<Mappings>();
        using var client_file = File.OpenText(Path.Combine(Folder, "matched_client.txt"));
        using var server_file = File.OpenText(Path.Combine(Folder, "matched_server.txt"));
        MappingsIO.ParseProguard(mojang.Client, client_file);
        MappingsIO.ParseProguard(mojang.Server, server_file);
        var mcp = ParseTsrgs(matched_version);
    }

    private Sided<Mappings> ParseTsrgs(string version)
    {
        var sided = new Sided<Mappings>();
        using var client_file = File.OpenText(Path.Combine(Folder, version, "client.tsrg"));
        using var server_file = File.OpenText(Path.Combine(Folder, version, "server.tsrg"));
        MappingsIO.ParseTsrg(sided.Client, client_file);
        MappingsIO.ParseTsrg(sided.Server, server_file);
        return sided;
    }

    public Sided<Mappings> CreateMappings(string version)
    {
        var mappings = ParseTsrgs(version);
        return mappings;
    }
}
