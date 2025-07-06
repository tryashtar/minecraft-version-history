namespace MinecraftVersionHistory;

public class ReleaseBranch
{
    private readonly List<VersionNode> VersionList;
    public ReadOnlyCollection<VersionNode> Versions => VersionList.AsReadOnly();
    public readonly string Name;
    public ReleaseBranch(VersionFacts facts, string name, IEnumerable<Version> versions)
    {
        Name = name;
        VersionList = versions.Select(x => new VersionNode(x, name)).OrderBy(x => x.Version, facts).ToList();
        for (int i = 0; i < VersionList.Count - 1; i++)
        {
            if (facts.Compare(VersionList[i].Version, VersionList[i + 1].Version) == 0)
                throw new ArgumentException($"Can't disambiguate order of {VersionList[i].Version} and {VersionList[i + 1].Version}");
        }
        for (int i = VersionList.Count - 1; i >= 1; i--)
        {
            VersionList[i].SetParent(VersionList[i - 1]);
        }
    }
}
