using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public class JavaUpdater : Updater
    {
        public JavaUpdater(AppConfig config) : base(config)
        { }

        protected override VersionConfig VersionConfig => Config.Java;

        protected override IEnumerable<Version> FindVersions()
        {
            foreach (var folder in Directory.EnumerateDirectories(VersionConfig.InputFolder))
            {
                yield return new JavaVersion(folder);
            }
        }
    }
}
