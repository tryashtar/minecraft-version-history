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
        Graph = CreateGraph();
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
        var git_versions = GitWrapper.CommittedVersions(OutputRepo, Config.GitInstallationPath).Select(x => new GitVersion(x));
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
    protected string OutputRepo => VersionConfig.OutputRepo;

    private void LoadCommits()
    {
        var versions = Graph.Flatten();
        CommitToVersion = new Dictionary<string, VersionNode>();
        VersionToCommit = new Dictionary<VersionNode, string>();
        Profiler.Start("Loading commits");
        var commits = GitWrapper.CommittedVersions(OutputRepo, Config.GitInstallationPath);
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

    private string GIT => $"\"{Config.GitInstallationPath}\"";

    private void InitialCommit(VersionNode version)
    {
        Profiler.Start("Initializing repo");
        CommandRunner.RunCommand(OutputRepo, $"{GIT} init");
        File.WriteAllText(Path.Combine(OutputRepo, ".gitignore"), Config.GitIgnoreContents);
        CommandRunner.RunCommand(OutputRepo, $"{GIT} add -A");
        CommandRunner.RunCommand(OutputRepo, $"{GIT} commit -m \"Initial commit\"");
        // create branch
        CommandRunner.RunCommand(OutputRepo, $"{GIT} branch \"{GetBranchName(version)}\"");
        CommandRunner.RunCommand(OutputRepo, $"{GIT} checkout \"{GetBranchName(version)}\"");
        Profiler.Stop();
        DoCommit(version);
    }

    private void InsertCommit(VersionNode version)
    {
        // find commit hash for existing version
        string hash = VersionToCommit[version.Parent];
        // create branch
        var branchname = GetBranchName(version);
        CommandRunner.RunCommand(OutputRepo, $"{GIT} branch \"{branchname}\" {hash}");
        // if this commit is the most recent for this branch, we can just commit right on top without insertion logic
        string tophash = CommandRunner.RunCommand(OutputRepo, $"{GIT} rev-parse \"{branchname}\"").Output;
        tophash = tophash.Substring(0, 40);
        if (hash == tophash)
        {
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
            Profiler.Start($"Rebasing");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} rebase temp \"{branchname}\" -X theirs");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} checkout \"{branchname}\"");
            CommandRunner.RunCommand(OutputRepo, $"{GIT} branch -d temp");
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
            MergeWithWorkspace(OutputRepo, workspace);
            Directory.Delete(workspace, true);
            Util.RemoveEmptyFolders(OutputRepo);
            File.WriteAllText(Path.Combine(OutputRepo, "version.txt"), version.Version.Name);
        });
        // commit
        Profiler.Start($"Running git commit");
        CommandRunner.RunCommand(OutputRepo, $"{GIT} add -A");
        CommandRunner.RunCommand(OutputRepo, $"set GIT_COMMITTER_DATE={version.Version.ReleaseTime} & {GIT} commit --date=\"{version.Version.ReleaseTime}\" -m \"{version.Version.Name}\"");
        string hash = CommandRunner.RunCommand(OutputRepo, $"{GIT} rev-parse HEAD").Output;
        hash = hash.Substring(0, 40);
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
