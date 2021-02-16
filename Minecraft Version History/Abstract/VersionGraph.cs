using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class VersionGraph
    {
        private VersionNode Root;
        private readonly Dictionary<string, VersionNode> BranchTips = new Dictionary<string, VersionNode>();
        public VersionGraph()
        {

        }

        public void Add(IVersionInfo version, string release)
        {
            var node = new VersionNode(version, release);
            if (Root == null)
            {
                Root = node;
            }
            else if (version.ReleaseTime < Root.Version.ReleaseTime)
            {
                Root.Parent = node;
                Root = node;
            }
            else
            {
                if (BranchTips.TryGetValue(release, out var latest))
                {
                    node.Parent = latest;
                }
                else
                {
                    node.Parent = Root;
                }
                BranchTips[release] = node;
            }
        }

        private class VersionNode
        {
            public readonly IVersionInfo Version;
            public readonly string ReleaseName;
            public VersionNode Parent;
            public VersionNode(IVersionInfo version, string release)
            {
                Version = version;
                ReleaseName = release;
            }
        }
    }
}
