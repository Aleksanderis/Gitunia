using System.Globalization;
using Gitunia.Git.Execution;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Fluent builder for throwaway, deterministic git repositories. Global and system git config are replaced by
/// an empty one, author and committer are fixed, and every commit gets the next minute after 2026-01-01T00:00:00Z,
/// so commit hashes are identical between runs and machines.
/// </summary>
public sealed class TestRepository : IDisposable
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _rootDirectory;
    private readonly string _emptyConfigPath;
    private int _commitCounter;

    private TestRepository(string rootDirectory, string path)
    {
        _rootDirectory = rootDirectory;
        WorkingTreePath = path;
        _emptyConfigPath = Path.Combine(rootDirectory, "empty.gitconfig");
        File.WriteAllText(_emptyConfigPath, string.Empty);
    }

    /// <summary>The working tree of the main repository.</summary>
    public string WorkingTreePath { get; }

    /// <summary>Creates an empty repository whose initial branch is <c>main</c>.</summary>
    public static TestRepository Create(bool bare = false)
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-repo-{Guid.NewGuid().ToString("N")}");
        var path = Path.Combine(root, "repo");
        Directory.CreateDirectory(path);
        var repository = new TestRepository(root, path);
        repository.Git(bare ? ["init", "-q", "--bare", "-b", "main"] : ["init", "-q", "-b", "main"]);
        return repository;
    }

    /// <summary>Writes <paramref name="files"/> (path, content), stages everything and commits.</summary>
    public TestRepository Commit(string message, params TestFile[] files)
    {
        foreach (var file in files)
        {
            WriteFile(file.RelativePath, file.Content);
        }

        Git("add", "-A");
        Git("commit", "-q", "--allow-empty", "-m", message);
        return this;
    }

    /// <summary>Deletes a file from the working tree (not staged).</summary>
    public TestRepository Delete(string relativePath)
    {
        File.Delete(Path.Combine(WorkingTreePath, relativePath));
        return this;
    }

    /// <summary>Writes a working-tree file, creating directories; not staged.</summary>
    public TestRepository WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(WorkingTreePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return this;
    }

    public TestRepository Branch(string name, string? startPoint = null)
    {
        Git(startPoint is null ? ["branch", name] : ["branch", name, startPoint]);
        return this;
    }

    public TestRepository Checkout(string reference)
    {
        Git("checkout", "-q", reference);
        return this;
    }

    public TestRepository CheckoutNewBranch(string name)
    {
        Git("checkout", "-q", "-b", name);
        return this;
    }

    public TestRepository Tag(string name)
    {
        Git("tag", name);
        return this;
    }

    /// <summary>Merges with <c>--no-ff</c> so a merge commit always exists.</summary>
    public TestRepository Merge(string branch)
    {
        Git("merge", "-q", "--no-ff", "-m", $"Merge {branch}", branch);
        return this;
    }

    /// <summary>Merges expecting a conflict; leaves the merge in progress.</summary>
    public TestRepository MergeExpectingConflict(string branch)
    {
        Git(["merge", "-q", "--no-ff", "-m", $"Merge {branch}", branch], acceptedExitCodes: [1]);
        return this;
    }

    /// <summary>Adds a linked worktree in a sibling directory and returns its path.</summary>
    public string AddWorktree(string name, string branch, bool createBranch = false)
    {
        var path = Path.Combine(_rootDirectory, "worktrees", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Git(createBranch ? ["worktree", "add", "-q", "-b", branch, path] : ["worktree", "add", "-q", path, branch]);
        return path;
    }

    /// <summary>Directory of the local bare repository created by <see cref="AddOrigin"/>.</summary>
    public string OriginPath => Path.Combine(_rootDirectory, "origin.git");

    /// <summary>Creates a local bare <c>origin</c> and pushes <c>main</c> with upstream tracking.</summary>
    public TestRepository AddOrigin()
    {
        Directory.CreateDirectory(OriginPath);
        GitIn(OriginPath, ["init", "-q", "--bare", "-b", "main"]);
        Git("remote", "add", "origin", OriginPath);
        Git("push", "-q", "-u", "origin", "main");
        return this;
    }

    /// <summary>Pushes <paramref name="branch"/> to origin and sets it as upstream.</summary>
    public TestRepository PushWithUpstream(string branch)
    {
        Git("push", "-q", "-u", "origin", branch);
        return this;
    }

    /// <summary>
    /// Adds a commit to <paramref name="branch"/> on origin only (through a throwaway clone), then fetches it here.
    /// </summary>
    public TestRepository CommitOnOrigin(string branch, string message, TestFile file)
    {
        var clone = Path.Combine(_rootDirectory, $"clone-{_commitCounter}");
        GitIn(_rootDirectory, ["clone", "-q", "--branch", branch, OriginPath, clone]);
        File.WriteAllText(Path.Combine(clone, file.RelativePath), file.Content);
        GitIn(clone, ["add", "-A"]);
        GitIn(clone, ["commit", "-q", "-m", message]);
        GitIn(clone, ["push", "-q", "origin", branch]);
        Git("fetch", "-q", "origin");
        return this;
    }

    /// <summary>Stashes a tracked change (<c>file.txt</c>) and an untracked file (<c>untracked.txt</c>).</summary>
    public TestRepository StashWithUntracked(string message = "wip")
    {
        WriteFile("file.txt", "stashed change\n");
        WriteFile("untracked.txt", "untracked\n");
        Git("stash", "push", "-q", "--include-untracked", "-m", message);
        return this;
    }

    /// <summary>Cherry-picks <paramref name="commit"/> expecting a conflict; leaves the pick in progress.</summary>
    public TestRepository CherryPickExpectingConflict(string commit)
    {
        Git(["cherry-pick", commit], acceptedExitCodes: [1]);
        return this;
    }

    /// <summary>Reverts <paramref name="commit"/> expecting a conflict; leaves the revert in progress.</summary>
    public TestRepository RevertExpectingConflict(string commit)
    {
        Git(["revert", "--no-edit", commit], acceptedExitCodes: [1]);
        return this;
    }

    /// <summary>Rebases onto <paramref name="onto"/> expecting a conflict; leaves the rebase stopped.</summary>
    public TestRepository RebaseExpectingConflict(string onto)
    {
        Git(["rebase", onto], acceptedExitCodes: [1]);
        return this;
    }

    /// <summary>Full hash of HEAD in the main repository.</summary>
    public string Head => Git("rev-parse", "HEAD").Trim();

    /// <summary>Runs git in the main repository and returns stdout. Commits advance the fake clock.</summary>
    public string Git(params string[] arguments) => Git(arguments, acceptedExitCodes: null);

    /// <summary>Same as <see cref="Git(string[])"/> but tolerates the given non-zero exit codes.</summary>
    public string Git(string[] arguments, IReadOnlyCollection<int>? acceptedExitCodes) =>
        GitIn(WorkingTreePath, arguments, acceptedExitCodes);

    /// <summary>Runs isolated git in <paramref name="directory"/> (a worktree, clone or bare repository).</summary>
    public string GitIn(string directory, string[] arguments, IReadOnlyCollection<int>? acceptedExitCodes = null)
    {
        // Every call moves the clock, so any history-writing command (commit, merge, cherry-pick, rebase, ...) has a distinct, ordered time.
        var runner = CreateRunner(Epoch.AddMinutes(_commitCounter++));
        var result = runner
            .RunAsync(directory, arguments, new GitRunOptions(AcceptedExitCodes: acceptedExitCodes))
            .GetAwaiter()
            .GetResult();
        return result.StandardOutputText;
    }

    /// <summary>
    /// A runner with the same isolated environment as <see cref="Git(string[])"/> (empty global and system config, fixed author and
    /// committer, no signing), for code under test that spawns git itself. Its commits get the next minute of the fake clock.
    /// </summary>
    public GitRunner CreateIsolatedRunner() => CreateRunner(Epoch.AddMinutes(_commitCounter++));

    private GitRunner CreateRunner(DateTimeOffset moment)
    {
        var stamp = moment.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return new GitRunner(new GitRunnerOptions(
            EnvironmentOverrides: new Dictionary<string, string?>
            {
                ["GIT_CONFIG_GLOBAL"] = _emptyConfigPath,
                ["GIT_CONFIG_SYSTEM"] = _emptyConfigPath,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_CONFIG_COUNT"] = "2",
                ["GIT_CONFIG_KEY_0"] = "commit.gpgsign",
                ["GIT_CONFIG_VALUE_0"] = "false",
                ["GIT_CONFIG_KEY_1"] = "core.autocrlf",
                ["GIT_CONFIG_VALUE_1"] = "false",
                ["GIT_AUTHOR_NAME"] = "Test",
                ["GIT_AUTHOR_EMAIL"] = "test@example.com",
                ["GIT_COMMITTER_NAME"] = "Test",
                ["GIT_COMMITTER_EMAIL"] = "test@example.com",
                ["GIT_DIR"] = null,
                ["GIT_WORK_TREE"] = null,
                ["GIT_INDEX_FILE"] = null,
                ["GIT_OBJECT_DIRECTORY"] = null,
                ["GIT_COMMON_DIR"] = null,
                ["GIT_AUTHOR_DATE"] = stamp,
                ["GIT_COMMITTER_DATE"] = stamp,
            }));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return;
        }

        // Object files are read-only, which blocks deletion on Windows. Elsewhere file names may not be valid UTF-8,
        // which .NET cannot address.
        if (OperatingSystem.IsWindows())
        {
            foreach (var file in Directory.EnumerateFiles(_rootDirectory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        try
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of a temp directory.
        }
    }
}
