namespace MinecraftVersionHistory;

public class SnapshotSpec
{
    private static readonly Regex SnapshotRegex = new(@"(?<year>\d\d)w(?<week>\d\d)(?<sub>.)");
    public readonly string Release;
    private readonly int Year;
    private readonly int FirstWeek;
    private readonly int LastWeek;
    private readonly bool HasWeeks;
    public SnapshotSpec(YamlMappingNode node)
    {
        Year = int.Parse((string)node["year"]);
        Release = (string)node["release"];
        if (node.TryGet("weeks") is not YamlSequenceNode weeks)
            HasWeeks = false;
        else
        {
            HasWeeks = true;
            FirstWeek = int.Parse((string)weeks.First());
            LastWeek = int.Parse((string)weeks.Last());
        }
    }

    public static bool IsSnapshot(Version version, out Snapshot snap)
    {
        var match = SnapshotRegex.Match(version.Name);
        snap = match.Success ? new Snapshot(match) : null;
        return match.Success;
    }

    public bool Matches(Snapshot snapshot)
    {
        if (snapshot.Year == this.Year)
        {
            if (!this.HasWeeks)
                return true;
            return snapshot.Week >= this.FirstWeek && snapshot.Week <= this.LastWeek;
        }
        return false;
    }
}

public class Snapshot
{
    public readonly int Year;
    public readonly int Week;
    public readonly char Subversion;
    public Snapshot(Match match)
    {
        Year = int.Parse(match.Groups["year"].Value) + 2000;
        Week = int.Parse(match.Groups["week"].Value);
        Subversion = match.Groups["sub"].Value.Single();
    }
}
