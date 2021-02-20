using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Version_History
{
    // hard things right now:
    // - how to get release name from version? should involve config regex thing
    // - how to extract data from version with context? java version needs values from config to extract itself
    // - how to get parent of version? should involve config parent skip
    // - port Energyxxer's slice merger
    // - move java key sorting rules to config
    public abstract class UpdaterOLD
    {
        protected Dictionary<Version, string> CommittedVersionDict;
        protected List<Version> UncommittedVersionList;
        protected abstract string RepoFolder { get; }

        public void Perform()
        {
            CreateVersionLists();
            foreach (var version in UncommittedVersionList)
            {
                Commit(version);
            }
        }

        private void Commit(Version version)
        {
            if (!UncommittedVersionList.Contains(version))
                return;
            var parent = GetParent(version);
            if (parent == null)
            {
                Console.WriteLine($"{version.Name} is the first version in the history!");
                InitialCommit(version);
            }
            else
            {
                Console.WriteLine($"Parent of {version.Name} is {parent.Name}");
                if (UncommittedVersionList.Contains(parent))
                {
                    Console.WriteLine("Need to commit parent first");
                    Commit(parent);
                }
                InsertCommit(version, parent);
            }
        }

        private void InitialCommit(Version version)
        {
            Console.WriteLine("Initializing repo...");
            CommandRunner.RunCommand(RepoFolder, $"git init");
            File.WriteAllText(Path.Combine(RepoFolder, ".gitignore"), "*.ini");
            CommandRunner.RunCommand(RepoFolder, $"git add -A");
            CommandRunner.RunCommand(RepoFolder, $"git commit -m \"Initial commit\"");
            // create branch
            var branchname = GetBranchName(version);
            CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\"");
            CommandRunner.RunCommand(RepoFolder, $"git checkout \"{branchname}\"");
            DoCommit(version);
        }

        private void DoCommit(Version version)
        {
            // extract
            string workspace = Path.Combine(Path.GetTempPath(), "mc_version_history_workspace");
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, true);
            Directory.CreateDirectory(workspace);
            Console.WriteLine($"Extracting {version}");
            //version.ExtractData(workspace);
            MergeWithWorkspace(RepoFolder, workspace);
            Directory.Delete(workspace, true);
            Util.RemoveEmptyFolders(RepoFolder);
            File.WriteAllText(Path.Combine(RepoFolder, "version.txt"), version.Name);
            // commit
            Console.WriteLine($"Committing...");
            CommandRunner.RunCommand(RepoFolder, $"git add -A");
            CommandRunner.RunCommand(RepoFolder, $"set GIT_COMMITTER_DATE={version.ReleaseTime} & git commit --date=\"{version.ReleaseTime}\" -m \"{version.Name}\"");
            // cleanup
            Console.WriteLine($"Cleaning up...");
            UncommittedVersionList.Remove(version);
            string hash = CommandRunner.RunCommand(RepoFolder, $"git rev-parse HEAD", output: true).Output;
            hash = hash.Substring(0, 40);
            CommittedVersionDict.Add(version, hash);
            CommandRunner.RunCommand(RepoFolder, $"git gc --prune=now --aggressive");
            CommandRunner.RunCommand(RepoFolder, $"git repack");
        }

        private string GetBranchName(Version version) => GetReleaseName(version).Replace(' ', '-');

        private void InsertCommit(Version version, Version parent)
        {
            Console.WriteLine($"Starting commit of {version}");
            // find commit hash for existing version
            string hash = CommittedVersionDict[parent];
            // create branch
            var branchname = GetBranchName(version);
            CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\" {hash}");
            // if this commit is the most recent for this branch, we can just commit right on top without insertion logic
            string tophash = CommandRunner.RunCommand(RepoFolder, $"git rev-parse \"{branchname}\"", output: true).Output;
            tophash = tophash.Substring(0, 40);
            if (hash == tophash)
            {
                Console.WriteLine($"On top, ready to go");
                CommandRunner.RunCommand(RepoFolder, $"git checkout \"{branchname}\"");
                DoCommit(version);
            }
            else
            {
                Console.WriteLine($"Needs to insert into history for this one");
                // make a branch that starts there and prepare to commit to it
                CommandRunner.RunCommand(RepoFolder, $"git checkout -b temp {hash}");
                CommandRunner.RunCommand(RepoFolder, $"git branch \"{branchname}\"");
                DoCommit(version);
                // insert
                Console.WriteLine($"Commit done, beginning rebase");
                CommandRunner.RunCommand(RepoFolder, $"git rebase --strategy-option theirs temp \"{branchname}\"");
                CommandRunner.RunCommand(RepoFolder, $"git checkout \"{branchname}\"");
                CommandRunner.RunCommand(RepoFolder, $"git branch -d temp");
                Console.WriteLine($"Rebase complete");
                // need to rescan since commit hashes change after a rebase
                CreateVersionLists();
            }
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

        private void CreateVersionLists()
        {
            CommittedVersionDict = new Dictionary<Version, string>();
            UncommittedVersionList = new List<Version>();
            Console.WriteLine("Scanning versions...");
            string all = CommandRunner.RunCommand(RepoFolder, $"git log --all", output: true).Output;
            foreach (var version in GetAllVersions())
            {
                int index = all.IndexOf($"\n\n    {version.Name}\n");
                if (index == -1)
                {
                    UncommittedVersionList.Add(version);
                    Console.WriteLine($"New version: {version.Name}");
                }
                else
                {
                    int hash_index = all.LastIndexOf("commit ", index);
                    string hash = all.Substring(hash_index + "commit ".Length, 40);
                    CommittedVersionDict.Add(version, hash);
                }
            }
        }

        protected abstract IEnumerable<Version> GetAllVersions();
        protected abstract Version GetParent(Version child);
        protected abstract string GetReleaseName(Version version);
    }
}
