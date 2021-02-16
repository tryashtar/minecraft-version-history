using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public abstract class Version
    {
        public DateTime ReleaseTime { get; protected set; }
        public string Name { get; protected set; }

        public override string ToString()
        {
            return $"{this.GetType().Name} {Name}, released {ReleaseTime}";
        }
    }
}
