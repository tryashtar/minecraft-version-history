using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinecraftVersionHistory
{
    public class BedrockUpdater : Updater
    {
        public BedrockUpdater(AppConfig config) : base(config)
        { }

        protected override VersionConfig VersionConfig => Config.Bedrock;

        protected override IEnumerable<Version> FindVersions()
        {
            foreach (var zip in Directory.EnumerateFiles(VersionConfig.InputFolder))
            {
                if (Path.GetExtension(zip) == ".zip")
                    yield return new BedrockVersion(zip);
            }
        }
    }
}
