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
            bool generated = FileSeemsGenerated(stream);
            if (SeemsGenerated != generated)
                return false;
        }
        return true;
    }

    private bool FileSeemsGenerated(StreamReader stream)
    {
        string line;
        int i = 0;
        while ((line = stream.ReadLine()) != null)
        {
            i++;
            int first_comma = line.IndexOf(',');
            int last_comma = line.LastIndexOf(',');
            if (first_comma != last_comma)
                return false;
        }
        return true;
    }
}
