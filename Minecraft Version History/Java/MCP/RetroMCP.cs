namespace MinecraftVersionHistory;

public class RetroMCP
{
    public readonly string Folder;
    private readonly Sided<Mappings> MatchedMCP;
    private readonly Sided<Mappings> MatchedMojang;
    private readonly VersionedRenames CustomRenames;
    public RetroMCP(string folder, string matched_version)
    {
        Folder = folder;
        var mcp = ParseTsrgs(matched_version);
        MatchedMCP = new(mcp.Client.Reversed(), mcp.Server.Reversed());
        MatchedMojang = new();
        using var client_file = File.OpenText(Path.Combine(Folder, "matched_client.txt"));
        using var server_file = File.OpenText(Path.Combine(Folder, "matched_server.txt"));
        MappingsIO.ParseProguard(MatchedMojang.Client, client_file);
        MappingsIO.ParseProguard(MatchedMojang.Server, server_file);
        CustomRenames = new((YamlSequenceNode)YamlHelper.ParseFile(Path.Combine(folder, "custom.yaml")));
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
        if (!Directory.Exists(Path.Combine(Folder, version)))
            return null;
        var final = new Sided<Mappings>();
        return final;
    }
}
