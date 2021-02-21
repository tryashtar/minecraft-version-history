using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace MinecraftVersionHistory
{
    public class SnapshotSpec
    {
        private static readonly Regex SnapshotRegex = new Regex(@"(?<year>\d\d)w(?<week>\d\d).");
        public readonly string Release;
        private readonly int Year;
        private readonly int FirstWeek;
        private readonly int LastWeek;
        private readonly bool HasWeeks;
        public SnapshotSpec(YamlMappingNode node)
        {
            Year = int.Parse((string)node["year"]);
            Release = (string)node["release"];
            var weeks = node.TryGet("weeks") as YamlSequenceNode;
            if (weeks == null)
                HasWeeks = false;
            else
            {
                HasWeeks = true;
                FirstWeek = int.Parse((string)weeks.First());
                LastWeek = int.Parse((string)weeks.Last());
            }
        }

        public static bool IsSnapshot(Version version, out Match match)
        {
            match = SnapshotRegex.Match(version.Name);
            return match.Success;
        }

        public bool Matches(Match match)
        {
            int year = int.Parse(match.Groups["year"].Value) + 2000;
            int week = int.Parse(match.Groups["week"].Value);
            if (year == Year)
            {
                if (!HasWeeks)
                    return true;
                return week >= FirstWeek && week <= LastWeek;
            }
            return false;
        }
    }
}
