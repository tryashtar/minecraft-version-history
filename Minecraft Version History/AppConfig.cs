namespace MinecraftVersionHistory;

public class AppConfig
{
    public readonly JavaConfig Java;
    public readonly BedrockConfig Bedrock;
    public readonly string GitInstallationPath;
    public readonly string GitIgnoreContents;
    public AppConfig(string folder, YamlMappingNode yaml)
    {
        GitInstallationPath = Util.FilePath(folder, yaml["git install"]);
        GitIgnoreContents = (string)yaml["gitignore"];
        Java = new JavaConfig(folder, yaml["java"] as YamlMappingNode);
        Bedrock = new BedrockConfig(folder, yaml["bedrock"] as YamlMappingNode);
    }
}
