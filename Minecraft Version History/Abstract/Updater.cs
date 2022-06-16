namespace MinecraftVersionHistory;

public abstract class Updater
{
    public readonly VersionGraph Graph;
    private Dictionary<string, VersionNode> CommitToVersion;
    private Dictionary<VersionNode, string> VersionToCommit;
    public readonly AppConfig Config;

    public Updater(AppConfig config)
    {
        Config = config;
        Profiler.Start("Building version graph");
        Graph = CreateGraph();
        Profiler.Stop();
    }

    public void Perform()
    {
        Console.WriteLine("Graph:");
        Console.WriteLine(Graph.ToString());
        var versions = Graph.Flatten();
        LoadCommits();
        foreach (var version in versions)
        {
            Commit(version);
        }
    }

    private VersionGraph CreateGraph()
    {
        var versions = FindVersions().Distinct(new DuplicateRemover()).ToList();
        var git_versions = VersionConfig.GitRepo.CommittedVersions().Select(x => new GitVersion(x));
        versions.AddRange(git_versions.Where(x => !versions.Any(y => x.Name == y.Name)));
        return new VersionGraph(VersionConfig.VersionFacts, versions);
    }

    private class DuplicateRemover : IEqualityComparer<Version>
    {
        public bool Equals(Version x, Version y)
        {
            return x.Name == y.Name && x.ReleaseTime == y.ReleaseTime;
        }

        public int GetHashCode([DisallowNull] Version obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    protected abstract VersionConfig VersionConfig { get; }
    private void LoadCommits()
    {
        var versions = Graph.Flatten().ToList();
        CommitToVersion = new Dictionary<string, VersionNode>();
        VersionToCommit = new Dictionary<VersionNode, string>();
        Profiler.Start("Loading commits");
        var commits = VersionConfig.GitRepo.CommittedVersions().ToList();
        foreach (var entry in commits)
        {
            var version = versions.FirstOrDefault(x => x.Version.Name == entry.Message);
            if (version != null)
            {
                CommitToVersion[entry.Hash] = version;
                VersionToCommit[version] = entry.Hash;
            }
        }
        Profiler.Stop();
    }

    private void Commit(VersionNode version)
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

    private string GetBranchName(VersionNode version) => version.ReleaseName.Replace(' ', '-');
    private void InitialCommit(VersionNode version)
    {
        Profiler.Start("Initializing repo");
        VersionConfig.GitRepo.Init();
        if (Config.GitIgnoreContents != null)
            File.WriteAllText(Path.Combine(VersionConfig.GitRepo.Folder, ".gitignore"), Config.GitIgnoreContents);
        // create branch
        VersionConfig.GitRepo.CheckoutBranch(GetBranchName(version));
        Profiler.Stop();
        DoCommit(version);
    }

    private void InsertCommit(VersionNode version)
    {
        // find commit hash for existing version
        string hash = VersionToCommit[version.Parent];
        // create branch
        var branchname = GetBranchName(version);
        VersionConfig.GitRepo.MakeBranch(branchname, hash);
        // if this commit is the most recent for this branch, we can just commit right on top without insertion logic
        string tophash = VersionConfig.GitRepo.BranchHash(branchname);
        if (hash == tophash)
        {
            VersionConfig.GitRepo.Checkout(branchname);
            DoCommit(version);
        }
        else
        {
            Console.WriteLine($"Needs to insert into history for this one");
            // make a branch that starts there and prepare to commit to it
            VersionConfig.GitRepo.CheckoutBranch("temp", hash);
            VersionConfig.GitRepo.MakeBranch(branchname);
            DoCommit(version);
            // insert
            Profiler.Start($"Rebasing");
            VersionConfig.GitRepo.Rebase("temp", branchname);
            VersionConfig.GitRepo.Checkout(branchname);
            VersionConfig.GitRepo.DeleteBranch("temp");
            Profiler.Stop();
            // need to rescan since commit hashes change after a rebase
            LoadCommits();
        }
    }

    private void DoCommit(VersionNode version)
    {
        Profiler.Start($"Adding commit for {version.Version}");
        // extract
        string workspace = Path.Combine(Path.GetTempPath(), "mc_version_history_workspace");
        if (Directory.Exists(workspace))
            Directory.Delete(workspace, true);
        Directory.CreateDirectory(workspace);
        Profiler.Run("Extracting", () =>
        { version.Version.ExtractData(workspace, Config); });
        Profiler.Run("Translating NBT Files", () =>
        { TranslateNbtFiles(workspace); });
        Profiler.Run($"Merging", () =>
        {
            MergeWithWorkspace(VersionConfig.GitRepo.Folder, workspace);
            Directory.Delete(workspace, true);
            Util.RemoveEmptyFolders(VersionConfig.GitRepo.Folder);
            File.WriteAllText(Path.Combine(VersionConfig.GitRepo.Folder, "version.txt"), version.Version.Name);
        });
        // commit
        Profiler.Start($"Running git commit");
        VersionConfig.GitRepo.Commit(version.Version.Name, version.Version.ReleaseTime);
        string hash = VersionConfig.GitRepo.BranchHash("HEAD");
        CommitToVersion.Add(hash, version);
        VersionToCommit.Add(version, hash);
        Profiler.Stop(); // git commit
        Profiler.Stop(); // top commit
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
        Profiler.Start("Deleting");
        foreach (var item in Directory.GetFiles(base_folder, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(base_folder, item);
            if (relative.StartsWith(".git"))
                continue;
            string workspace_version = Path.Combine(workspace, relative);
            if (!File.Exists(workspace_version))
                File.Delete(item);
        }
        Profiler.Stop();

        // copy new/changed files from workspace
        Profiler.Start("Copying");
        foreach (var item in Directory.GetFiles(workspace, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(workspace, item);
            string base_version = Path.Combine(base_folder, relative);
            if (!File.Exists(base_version) || !Util.FilesAreEqual(new FileInfo(item), new FileInfo(base_version)))
                Util.Copy(item, base_version);
            File.Delete(item);
        }
        Profiler.Stop();
    }

    protected abstract IEnumerable<Version> FindVersions();
}
