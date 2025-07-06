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
        GitIgnoreContents = yaml.Go("gitignore").String();
        Java = new JavaConfig(folder, this, yaml["java"] as YamlMappingNode);
        Bedrock = new BedrockConfig(folder, this, yaml["bedrock"] as YamlMappingNode);
    }
}
