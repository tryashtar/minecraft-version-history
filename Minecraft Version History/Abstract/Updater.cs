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
        private Dictionary<string, IVersionNode> CommitToVersion;
        private Dictionary<IVersionNode, string> VersionToCommit;
        protected abstract Config Config { get; }
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

        private void LoadCommits()
        {
            var versions = Graph.Flatten();
            CommitToVersion = new Dictionary<string, IVersionNode>();
            VersionToCommit = new Dictionary<IVersionNode, string>();
            Console.WriteLine("Loading commits...");
            string[] all = CommandRunner.RunCommand(Config.OutputRepo, "git log --all --pretty=\"%H %s\"", output: true).Output.Split('\n');
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

        private void InitialCommit(IVersionNode version)
        {
            Console.WriteLine("Initializing repo...");
            CommandRunner.RunCommand(Config.OutputRepo, $"git init");
            File.WriteAllText(Path.Combine(Config.OutputRepo, ".gitignore"), "*.ini");
            CommandRunner.RunCommand(Config.OutputRepo, $"git add -A");
            CommandRunner.RunCommand(Config.OutputRepo, $"git commit -m \"Initial commit\"");
            // create branch
            CommandRunner.RunCommand(Config.OutputRepo, $"git branch \"{GetBranchName(version)}\"");
            CommandRunner.RunCommand(Config.OutputRepo, $"git checkout \"{GetBranchName(version)}\"");
            DoCommit(version);
        }

        private void InsertCommit(IVersionNode version)
        {
            Console.WriteLine($"Starting commit of {version.Version}");
            // find commit hash for existing version
            string hash = VersionToCommit[version.Parent];
            // create branch
            var branchname = GetBranchName(version);
            CommandRunner.RunCommand(Config.OutputRepo, $"git branch \"{branchname}\" {hash}");
            // if this commit is the most recent for this branch, we can just commit right on top without insertion logic
            string tophash = CommandRunner.RunCommand(Config.OutputRepo, $"git rev-parse \"{branchname}\"", output: true).Output;
            tophash = tophash.Substring(0, 40);
            if (hash == tophash)
            {
                Console.WriteLine($"On top, ready to go");
                CommandRunner.RunCommand(Config.OutputRepo, $"git checkout \"{branchname}\"");
                DoCommit(version);
            }
            else
            {
                Console.WriteLine($"Needs to insert into history for this one");
                // make a branch that starts there and prepare to commit to it
                CommandRunner.RunCommand(Config.OutputRepo, $"git checkout -b temp {hash}");
                CommandRunner.RunCommand(Config.OutputRepo, $"git branch \"{branchname}\"");
                DoCommit(version);
                // insert
                Console.WriteLine($"Commit done, beginning rebase");
                CommandRunner.RunCommand(Config.OutputRepo, $"git rebase temp \"{branchname}\" -X theirs");
                CommandRunner.RunCommand(Config.OutputRepo, $"git checkout \"{branchname}\"");
                CommandRunner.RunCommand(Config.OutputRepo, $"git branch -d temp");
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
            MergeWithWorkspace(Config.OutputRepo, workspace);
            Directory.Delete(workspace, true);
            Util.RemoveEmptyFolders(Config.OutputRepo);
            File.WriteAllText(Path.Combine(Config.OutputRepo, "version.txt"), version.Version.Name);
            // commit
            Console.WriteLine($"Committing...");
            CommandRunner.RunCommand(Config.OutputRepo, $"git add -A");
            CommandRunner.RunCommand(Config.OutputRepo, $"set GIT_COMMITTER_DATE={version.Version.ReleaseTime} & git commit --date=\"{version.Version.ReleaseTime}\" -m \"{version.Version.Name}\"");
            // cleanup
            Console.WriteLine($"Cleaning up...");
            string hash = CommandRunner.RunCommand(Config.OutputRepo, $"git rev-parse HEAD", output: true).Output;
            hash = hash.Substring(0, 40);
            CommitToVersion.Add(hash, version);
            VersionToCommit.Add(version, hash);
            CommandRunner.RunCommand(Config.OutputRepo, $"git gc --prune=now --aggressive");
            CommandRunner.RunCommand(Config.OutputRepo, $"git repack");
        }

        private void MergeWithWorkspace(string base_folder, string workspace)
        {
            // delete files that are not present in workspace
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
