namespace MinecraftVersionHistory;

public class GitRepo
{
    public readonly string Folder;
    private readonly string GitInstall;

    public GitRepo(string repo_folder, string git_install)
    {
        Folder = repo_folder;
        GitInstall = git_install;
    }

    public ProcessResult Run(string args, TextWriter output, TextWriter error)
    {
        return CommandRunner.RunCommand(Folder, GitInstall, args, output, error);
    }

    public ProcessResult Run(string args)
    {
        return Run(args, Console.Out, Console.Out);
    }

    public void Init()
    {
        Run("init");
    }

    public void Commit(string message, DateTime? date = null)
    {
        Run("add -A");
        if (date == null)
            Run($"commit -m \"{message}\"");
        else
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_DATE", date.ToString());
            Run($"commit --date=\"{date}\" -m \"{message}\"");
        }
    }

    public void CheckoutBranch(string branch, string? hash = null)
    {
        if (hash == null)
            Run($"checkout -b \"{branch}\"");
        else
            Run($"checkout -b \"{branch}\" {hash}");
    }

    public void Checkout(string branch)
    {
        Run($"checkout \"{branch}\"");
    }

    public void MakeBranch(string branch, string? hash = null)
    {
        if (hash == null)
            Run($"branch \"{branch}\"");
        else
            Run($"branch \"{branch}\" {hash}");
    }

    public void DeleteBranch(string branch)
    {
        Run($"branch -D \"{branch}\"");
    }

    public string BranchHash(string branch)
    {
        return Run($"rev-parse \"{branch}\"", null, Console.Out).Output[..40];
    }

    public void Rebase(string from, string to)
    {
        Run($"rebase \"{from}\" \"{to}\" -X theirs");
    }

    public IEnumerable<GitCommit> CommittedVersions()
    {
        string[] all = StringUtils.SplitLines(Run(
            "log --exclude=\"refs/notes/*\" --all --pretty=\"%H___%s___%ad___%p\" --date=format:\"%Y/%m/%d\"",
            null, Console.Out).Output).ToArray();
        foreach (var item in all)
        {
            if (String.IsNullOrEmpty(item))
                continue;
            var entries = item.Split("___");
            yield return new GitCommit(entries[0], entries[1], DateTime.Parse(entries[2]));
        }
    }
}

public class GitCommit
{
    public readonly string Hash;
    public readonly string Message;
    public readonly DateTime CommitTime;

    public GitCommit(string hash, string message, DateTime time)
    {
        Hash = hash;
        Message = message;
        CommitTime = time;
    }
}