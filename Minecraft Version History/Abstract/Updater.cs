using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    public abstract class Updater
    {
        public VersionGraph Graph { get; private set; }
        public void Perform()
        {
            Graph = CreateGraph();
            Console.WriteLine("Graph:");
            Console.WriteLine(Graph.ToString());
        }

        protected abstract VersionGraph CreateGraph();
    }
}
