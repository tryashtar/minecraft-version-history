namespace MinecraftVersionHistory;

public class VersionFacts : IComparer<Version>
{
    private readonly List<Regex> SkipVersions;
    private readonly List<Regex> InsaneBranches;
    private readonly List<Regex> InsaneVersions;
    private readonly Dictionary<string, string> ParentsMap;
    private readonly Dictionary<string, string> CustomNameMap;
    private readonly Dictionary<Regex, string> RegexReleases;
    private readonly List<SnapshotSpec> SnapshotReleases;
    private readonly List<string> VersionOrder;
    private readonly List<OrderMethod> OrderPriority;
    public VersionFacts(YamlMappingNode yaml)
    {
        SkipVersions = yaml.Go("skip").ToList(x => new Regex((string)x)) ?? new();
        InsaneBranches = yaml.Go("insane", "releases").ToList(x => new Regex((string)x)) ?? new();
        InsaneVersions = yaml.Go("insane", "versions").ToList(x => new Regex((string)x)) ?? new();
        ParentsMap = yaml.Go("parents").ToDictionary() ?? new();
        CustomNameMap = yaml.Go("names").ToDictionary() ?? new();
        RegexReleases = yaml.Go("releases", "regex").ToDictionary(x => new Regex((string)x), x => (string)x) ?? new();
        SnapshotReleases = yaml.Go("releases", "snapshots").ToList(x => new SnapshotSpec((YamlMappingNode)x)) ?? new();
        VersionOrder = yaml.Go("ordering", "versions").ToStringList() ?? new();
        OrderPriority = yaml.Go("ordering", "priority").ToList(x => StringUtils.ParseUnderscoredEnum<OrderMethod>((string)x)) ?? new() { OrderMethod.ListedOrder, OrderMethod.ReleaseTime, OrderMethod.BestGuess };
    }

    public bool ShouldSkip(Version version)
    {
        foreach (var item in SkipVersions)
        {
            if (item.IsMatch(version.Name))
                return true;
        }
        return false;
    }

    public string? CustomName(string file)
    {
        CustomNameMap.TryGetValue(file, out var result);
        return result;
    }

    public bool IsInsaneRelease(string release)
    {
        return InsaneBranches.Any(x => x.IsMatch(release));
    }

    public bool IsInsaneVersion(Version version)
    {
        return InsaneVersions.Any(x => x.IsMatch(version.Name));
    }

    public string GetReleaseName(Version version)
    {
        foreach (var candidate in RegexReleases)
        {
            if (candidate.Key.IsMatch(version.Name))
                return candidate.Key.Replace(version.Name, candidate.Value);
        }
        if (SnapshotSpec.IsSnapshot(version, out var snap))
        {
            foreach (var candidate in SnapshotReleases)
            {
                if (candidate.Matches(snap))
                    return candidate.Release;
            }
        }
        throw new KeyNotFoundException($"What release is {version} for?");
    }

    public string SpecialParent(Version version)
    {
        ParentsMap.TryGetValue(version.Name, out var result);
        return result;
    }

    private enum OrderMethod
    {
        ReleaseTime,
        BestGuess,
        ListedOrder
    }

    public int Compare(Version? x, Version? y)
    {
        if (x == y)
            return 0;
        foreach (var method in OrderPriority)
        {
            int order = 0;
            if (method == OrderMethod.BestGuess)
                order = BestGuessCompare(x, y);
            else if (method == OrderMethod.ReleaseTime)
                order = x.ReleaseTime.CompareTo(y.ReleaseTime);
            else if (method == OrderMethod.ListedOrder)
            {
                int x_index = VersionOrder.IndexOf(x.Name);
                int y_index = VersionOrder.IndexOf(y.Name);
                if (x_index != -1 && y_index != -1)
                    order = x_index.CompareTo(y_index);
            }
            if (order != 0)
                return order;
        }
        throw new ArgumentException($"Can't tell which came first: {x} or {y}");
    }

    private static readonly char[] TypicalSplits = new char[] { ' ', '-', '.', '_' };
    private int BestGuessCompare(Version n1, Version n2)
    {
        bool is_snap1 = SnapshotSpec.IsSnapshot(n1, out var snap1);
        bool is_snap2 = SnapshotSpec.IsSnapshot(n2, out var snap2);
        // if they're both snapshots, compare snapshot data
        if (is_snap1 && is_snap2)
        {
            int year_compare = snap1.Year.CompareTo(snap2.Year);
            if (year_compare != 0)
                return year_compare;
            int week_compare = snap1.Week.CompareTo(snap2.Week);
            if (week_compare != 0)
                return week_compare;
            int sub_compare = snap1.Subversion.CompareTo(snap2.Subversion);
            if (sub_compare != 0)
                return sub_compare;
        }
        // assume releases always follow snapshots
        if (is_snap1 && !is_snap2)
            return 1;
        if (is_snap2 && !is_snap1)
            return -1;
        string[] n1_split = n1.Name.Split(TypicalSplits, StringSplitOptions.RemoveEmptyEntries);
        string[] n2_split = n2.Name.Split(TypicalSplits, StringSplitOptions.RemoveEmptyEntries);
        int[] n1_nums = n1_split.Select(x => FindNumber(x)).ToArray();
        int[] n2_nums = n2_split.Select(x => FindNumber(x)).ToArray();
        for (int i = 0; i < Math.Min(n1_nums.Length, n2_nums.Length); i++)
        {
            if (n1_nums[i] == -1 && n2_nums[i] == -1)
                continue;
            if (n1_nums[i] == -1)
                return 1;
            if (n2_nums[i] == -1)
                return -1;
            int compare = n1_nums[i].CompareTo(n2_nums[i]);
            if (compare != 0)
                return compare;
        }
        for (int i = 0; i < Math.Min(n1_split.Length, n2_split.Length); i++)
        {
            int compare = n1_split[i].CompareTo(n2_split[i]);
            if (compare != 0)
                return compare;
        }
        // assume shorter strings come first, e.g. 1.2.3-ex > 1.2.3
        int size_compare = n1_split.Length.CompareTo(n2_split.Length);
        if (size_compare != 0)
            return size_compare;
        return 0;
    }

    private static readonly Regex NumberFinder = new(@"\d+");
    private static int FindNumber(string str)
    {
        var match = NumberFinder.Match(str);
        if (!match.Success)
            return -1;
        return int.Parse(match.Value);
    }
}
