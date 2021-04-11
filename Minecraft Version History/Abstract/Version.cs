using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public abstract class Version
    {
        public DateTime ReleaseTime { get; protected set; }
        public string Name { get; protected set; }

        public override string ToString()
        {
            return $"{this.GetType().Name} {Name}, released {ReleaseTime}";
        }

        public abstract void ExtractData(string folder, AppConfig config);
    }
}
