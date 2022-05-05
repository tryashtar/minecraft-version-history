namespace MinecraftVersionHistory;

public class SorterRequirements
{
    public DateTime? After { get; init; }
    public bool? SeemsGenerated { get; init; }
    public SorterRequirements()
    { }
    public SorterRequirements(YamlMappingNode node)
    {
        After = node.Go("after").NullableStructParse(x => DateTime.Parse(x.String()));
        SeemsGenerated = node.Go("seems_generated").NullableStructParse(x => Boolean.Parse(x.String()));
    }

    public bool MetBy(Version version)
    {
        if (After != null && version.ReleaseTime < After.Value)
            return false;
        return true;
    }

    public bool MetBy(string path)
    {
        if (SeemsGenerated != null)
        {
            using var stream = File.OpenText(path);
            string line;
            bool seems_generated = true;
            int i = 0;
            while ((line = stream.ReadLine()) != null)
            {
                i++;
                if ((i == 2 && line.StartsWith("    \"")) || line.IndexOf(',') != line.LastIndexOf(','))
                {
                    seems_generated = false;
                    break;
                }
            }
            if (SeemsGenerated != seems_generated)
                return false;
        }
        return true;
    }
}
