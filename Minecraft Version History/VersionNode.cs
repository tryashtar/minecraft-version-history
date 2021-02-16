using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public class VersionNode
    {
        public readonly Version Version;
        public VersionNode(Version version)
        {
            Version = version;
        }
    }
}
