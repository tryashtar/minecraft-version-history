using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public interface IVersionNode
    {
        IVersionNode Parent { get; }
        IEnumerable<IVersionNode> Children { get; }
        Version Version { get; }
        string ReleaseName { get; }
    }
}
