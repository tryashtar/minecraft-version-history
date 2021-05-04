using fNbt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TryashtarUtils.Nbt;

namespace MinecraftVersionHistory
{
    public abstract class Updater
    {
        public VersionGraph Graph { get; private set; }
        private Dictionary<string, IVersionNode> CommitToVersion;
        private Dictionary<IVersionNode, string> VersionToCommit;
        public readonly AppConfig Config;

        public Updater(AppConfig config)
        {
            Config = config;
        }

        public void Perform()
        {
            Graph = CreateGraph();
            Console.WriteLine("Graph:");
            Console.WriteLine(Graph.ToString());
            var versions = Graph.Flatten();
            LoadCommits();
            foreach (var version in versions)
            {
                Commit(version);
            }
        }

        protected abstract VersionConfig VersionConfig { get; }
        protected string OutputRepo => VersionConfig.OutputRepo;

        private void LoadCommits()
        {
            var versions = Graph.Flatten();
            CommitToVersion = new Dictionary<string, IVersionNode>();
            VersionToCommit = new Dictionary<IVersionNode, string>();
            Console.WriteLine("Loading commits...");
            string[] all = CommandRunner.RunCommand(OutputRepo, $"{GIT} log --all --pretty=\"%H %s\"", output: true).Output.Split('\n');
            foreach (var entry in all)
            {
                if (String.IsNullOrEmpty(entry))
                    continue;
                string hash = entry.Substring(0, 40);
                string message = entry.Substring(41);
                var version = versions.FirstOrDefault(x => x.Version.Name == message);
                if (version != null)
                {
                    CommitToVersion[hash] = version;
                    VersionToCommit[version] = hash;
                }
            }
        }

        private void Commit(IVersionNode version)
        {
            if (VersionToCommit.ContainsKey(version))
                return;
            if (version.Parent == null)
            {
                Console.WriteLine($"{version.Version.Name} is the first version in the history!");
                InitialCommit(version);
            }
            else
            {
                Console.WriteLine($"Parent of {version.Version.Name} is {version.Parent.Version.Name}");
                if (!VersionToCommit.ContainsKey(version.Parent))
                {
                    Console.WriteLine("Need to commit parent first");
                    Commit(version.Parent);
                }
                InsertCommit(version);
            }
        }

        private string GetBranchName(IVersionNode version) => version.ReleaseName.Replace(' ', '-');

        private string GIT => $"\"{Config.GitInstallationPath}\"";

        private void InitialCommit(IVersionNode version)
        {
            Console.WriteLine("Initializing repo...");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} init");
            File.WriteAllText(Path.Combine(OutputRepo, ".gitignore"), Config.GitIgnoreContents);
            CommandRunner.RunCommand(OutputRepo, $"{GIT} add -A");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} commit -m \"Initial commit\"");
            // create branch
            CommandRunner.RunCommand(OutputRepo, $"{GIT} branch \"{GetBranchName(version)}\"");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} checkout \"{GetBranchName(version)}\"");
            DoCommit(version);
        }

        private void InsertCommit(IVersionNode version)
        {
            Console.WriteLine($"Starting commit of {version.Version}");
            // find commit hash for existing version
            string hash = VersionToCommit[version.Parent];
            // create branch
            var branchname = GetBranchName(version);
            CommandRunner.RunCommand(OutputRepo, $"{GIT} branch \"{branchname}\" {hash}");
            // if this commit is the most recent for this branch, we can just commit right on top without insertion logic
            string tophash = CommandRunner.RunCommand(OutputRepo, $"{GIT} rev-parse \"{branchname}\"", output: true).Output;
            tophash = tophash.Substring(0, 40);
            if (hash == tophash)
            {
                Console.WriteLine($"On top, ready to go");
                CommandRunner.RunCommand(OutputRepo, $"{GIT} checkout \"{branchname}\"");
                DoCommit(version);
            }
            else
            {
                Console.WriteLine($"Needs to insert into history for this one");
                // make a branch that starts there and prepare to commit to it
                CommandRunner.RunCommand(OutputRepo, $"{GIT} checkout -b temp {hash}");
                CommandRunner.RunCommand(OutputRepo, $"{GIT} branch \"{branchname}\"");
                DoCommit(version);
                // insert
                Console.WriteLine($"Commit done, beginning rebase");
                CommandRunner.RunCommand(OutputRepo, $"{GIT} rebase temp \"{branchname}\" -X theirs");
                CommandRunner.RunCommand(OutputRepo, $"{GIT} checkout \"{branchname}\"");
                CommandRunner.RunCommand(OutputRepo, $"{GIT} branch -d temp");
                Console.WriteLine($"Rebase complete");
                // need to rescan since commit hashes change after a rebase
                LoadCommits();
            }
        }

        private void DoCommit(IVersionNode version)
        {
            // extract
            string workspace = Path.Combine(Path.GetTempPath(), "mc_version_history_workspace");
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, true);
            Directory.CreateDirectory(workspace);
            Console.WriteLine($"Extracting {version.Version}");
            version.Version.ExtractData(workspace, Config);
            Console.WriteLine($"Translating NBT files...");
            TranslateNbtFiles(workspace);
            Console.WriteLine($"Merging... ({version.Version})");
            MergeWithWorkspace(OutputRepo, workspace);
            Directory.Delete(workspace, true);
            Util.RemoveEmptyFolders(OutputRepo);
            File.WriteAllText(Path.Combine(OutputRepo, "version.txt"), version.Version.Name);
            // commit
            Console.WriteLine($"Committing...");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} add -A");
            CommandRunner.RunCommand(OutputRepo, $"set GIT_COMMITTER_DATE={version.Version.ReleaseTime} & {GIT} commit --date=\"{version.Version.ReleaseTime}\" -m \"{version.Version.Name}\"");
            // cleanup
            Console.WriteLine($"Cleaning up... ({version.Version})");
            string hash = CommandRunner.RunCommand(OutputRepo, $"{GIT} rev-parse HEAD", output: true).Output;
            hash = hash.Substring(0, 40);
            CommitToVersion.Add(hash, version);
            VersionToCommit.Add(version, hash);
        }

        // read all NBT files in the version and write textual copies
        private void TranslateNbtFiles(string root_folder)
        {
            // don't enumerate because we're creating new files as we go
            foreach (var path in Directory.GetFiles(root_folder, "*", SearchOption.AllDirectories))
            {
                VersionConfig.TranslateNbt(path);
            }
        }

        private void MergeWithWorkspace(string base_folder, string workspace)
        {
            // delete files that are not present in workspace
            Console.WriteLine("Deleting...");
            foreach (var item in Directory.GetFiles(base_folder, "*", SearchOption.AllDirectories))
            {
                string relative = Util.RelativePath(base_folder, item);
                if (relative.StartsWith(".git"))
                    continue;
                string workspace_version = Path.Combine(workspace, relative);
                if (!File.Exists(workspace_version))
                    File.Delete(item);
            }

            // copy new/changed files from workspace
            Console.WriteLine("Copying...");
            foreach (var item in Directory.GetFiles(workspace, "*", SearchOption.AllDirectories))
            {
                string relative = Util.RelativePath(workspace, item);
                string base_version = Path.Combine(base_folder, relative);
                if (!File.Exists(base_version) || !Util.FilesAreEqual(new FileInfo(item), new FileInfo(base_version)))
                    Util.Copy(item, base_version);
                File.Delete(item);
            }
        }

        protected abstract VersionGraph CreateGraph();
    }
}
