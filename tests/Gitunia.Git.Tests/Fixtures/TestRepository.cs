using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly ConcurrentDictionary<string, Lazy<TestRepository>> Templates = new();

    private static readonly int ProbeTimeout = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

    private readonly string _rootDirectory;
    private readonly string _emptyConfigPath;
    private int _commitCounter;

    static TestRepository() => AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteTemplates();

    private TestRepository(string rootDirectory, string path, int commitCounter = 0)
    {
        _rootDirectory = rootDirectory;
        _commitCounter = commitCounter;
        WorkingTreePath = path;
        _emptyConfigPath = Path.Combine(rootDirectory, "empty.gitconfig");
        File.WriteAllText(_emptyConfigPath, string.Empty);
    }

    /// <summary>The working tree of the main repository.</summary>
    public string WorkingTreePath { get; }

    /// <summary>
    /// Creates an empty repository whose initial branch is <c>main</c>. It is a copy of one <c>git init</c> made per
    /// process (a git process costs far more on macOS and Windows than copying a few files, #1388).
    /// </summary>
    public static TestRepository Create(bool bare = false) =>
        FromTemplate(bare ? "empty-bare" : "empty", () => CreateByInit(bare));

    /// <summary>
    /// Builds the repository with <paramref name="build"/> once per test process, then hands every caller its own copy
    /// (same files, same commit hashes, same fake clock position) without spawning git again. The copy's absolute
    /// paths (origin remote, linked worktrees) are rewritten to its own directory. <paramref name="build"/> must be
    /// deterministic and must not touch state outside the repository's own directory.
    /// </summary>
    public static TestRepository FromTemplate(string key, Func<TestRepository> build)
    {
        var template = Templates.GetOrAdd(key, _ => new Lazy<TestRepository>(build)).Value;

        return template.CopyToNewRoot();
    }

    /// <summary>
    /// Creates a repository with one commit of <paramref name="files"/>, equal to
    /// <c>Create().Commit(message, files)</c> but built once per process for each distinct message and file set,
    /// then copied (<see cref="FromTemplate"/>, #1409).
    /// </summary>
    public static TestRepository CreateCommitted(string message, params TestFile[] files)
    {
        var description = new StringBuilder(message);
        foreach (var file in files)
        {
            description.Append('\0').Append(file.RelativePath).Append('\0').Append(file.Content);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(description.ToString()));

        return FromTemplate($"committed-{Convert.ToHexString(hash)}", () => Create().Commit(message, files));
    }

    /// <summary>The directory of the linked worktree added with <see cref="AddWorktree"/> under <paramref name="name"/>.</summary>
    public string WorktreePath(string name) => Path.Combine(_rootDirectory, "worktrees", name);

    /// <summary>Whether the installed git can create reftable repositories (2.45 and later); tests skip otherwise.</summary>
    public static bool IsReftableSupported { get; } = ProbeReftableSupport();

    /// <summary>Creates an empty reftable-backed repository (<c>--ref-format=reftable</c>), initial branch <c>main</c>.</summary>
    public static TestRepository CreateReftable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-repo-{Guid.NewGuid().ToString("N")}");
        var path = Path.Combine(root, "repo");
        Directory.CreateDirectory(path);
        var repository = new TestRepository(root, path);
        repository.Git("init", "-q", "--ref-format=reftable", "-b", "main");
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
        var path = WorktreePath(name);
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
        // The counter advances with every git call, so each clone directory is unique.
        var clone = Path.Combine(_rootDirectory, $"clone-{_commitCounter}");
        GitIn(_rootDirectory, ["clone", "-q", "--branch", branch, OriginPath, clone]);
        File.WriteAllText(Path.Combine(clone, file.RelativePath), file.Content);
        GitIn(clone, ["add", "-A"]);
        GitIn(clone, ["commit", "-q", "-m", message]);
        GitIn(clone, ["push", "-q", "origin", branch]);
        DeleteDirectory(clone);
        Git("fetch", "-q", "origin");
        return this;
    }

    /// <summary>
    /// Stashes a change to the existing tracked <c>file.txt</c> (as in Linear) and an untracked <c>untracked.txt</c>.
    /// </summary>
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
    public GitRunner CreateIsolatedRunner(IReadOnlyDictionary<string, string?>? extraEnvironment = null) =>
        CreateRunner(Epoch.AddMinutes(_commitCounter++), extraEnvironment);

    private static void DeleteTemplates()
    {
        foreach (var template in Templates.Values)
        {
            if (template.IsValueCreated)
            {
                DeleteDirectory(template.Value._rootDirectory);
            }
        }
    }

    private static TestRepository CreateByInit(bool bare)
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-repo-{Guid.NewGuid().ToString("N")}");
        var path = Path.Combine(root, "repo");
        Directory.CreateDirectory(path);
        var repository = new TestRepository(root, path);
        repository.Git(bare ? ["init", "-q", "--bare", "-b", "main"] : ["init", "-q", "-b", "main"]);
        return repository;
    }

    private TestRepository CopyToNewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-repo-{Guid.NewGuid().ToString("N")}");
        CopyDirectory(_rootDirectory, root);
        RewriteAbsolutePaths(root);

        return new TestRepository(root, Path.Combine(root, "repo"), _commitCounter);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    // The files git writes absolute paths into: the remote URL of the repository and the links between the main
    // repository and its linked worktrees (git writes them with forward slashes on Windows).
    private void RewriteAbsolutePaths(string newRoot)
    {
        var candidates = new List<string>
        {
            Path.Combine(newRoot, "repo", ".git", "config"),
            Path.Combine(newRoot, "origin.git", "config"),
        };
        var worktreeAdmin = Path.Combine(newRoot, "repo", ".git", "worktrees");
        if (Directory.Exists(worktreeAdmin))
        {
            candidates.AddRange(Directory.EnumerateFiles(worktreeAdmin, "gitdir", SearchOption.AllDirectories));
        }

        var worktrees = Path.Combine(newRoot, "worktrees");
        if (Directory.Exists(worktrees))
        {
            candidates.AddRange(Directory.EnumerateFiles(worktrees, ".git", SearchOption.AllDirectories));
        }

        foreach (var file in candidates.Where(File.Exists))
        {
            var text = File.ReadAllText(file);
            var rewritten = text
                .Replace(_rootDirectory, newRoot, PathComparison)
                .Replace(Slashed(_rootDirectory), Slashed(newRoot), PathComparison)
                .Replace(Escaped(_rootDirectory), Escaped(newRoot), PathComparison);
            if (rewritten != text)
            {
                // Git marks the .git file of a linked worktree hidden on Windows, which makes a plain write fail.
                var attributes = File.GetAttributes(file);
                File.SetAttributes(file, FileAttributes.Normal);
                File.WriteAllText(file, rewritten);
                File.SetAttributes(file, attributes);
            }
        }
    }

    // Git writes a backslash in a config value as two (the remote URL of the repository on Windows).
    private static string Escaped(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static string Slashed(string path) => path.Replace('\\', '/');

    private GitRunner CreateRunner(DateTimeOffset moment, IReadOnlyDictionary<string, string?>? extraEnvironment = null)
    {
        var stamp = moment.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var settings = new List<KeyValuePair<string, string>>
        {
            new("commit.gpgsign", "false"),
            new("core.autocrlf", "false"),
        };
        settings.AddRange(TestGitSpeed.Settings);
        var environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = _emptyConfigPath,
            ["GIT_CONFIG_SYSTEM"] = _emptyConfigPath,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_COUNT"] = settings.Count.ToString(CultureInfo.InvariantCulture),
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
        };
        for (var index = 0; index < settings.Count; index++)
        {
            environment[$"GIT_CONFIG_KEY_{index}"] = settings[index].Key;
            environment[$"GIT_CONFIG_VALUE_{index}"] = settings[index].Value;
        }

        foreach (var (name, value) in extraEnvironment ?? new Dictionary<string, string?>())
        {
            environment[name] = value;
        }

        return new GitRunner(
            new GitRunnerOptions(TestGitSpeed.Executable, TestGitSpeed.Timeout, environment));
    }

    public void Dispose() => DeleteDirectory(_rootDirectory);


    private static bool ProbeReftableSupport()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-probe-{Guid.NewGuid().ToString("N")}");
        try
        {
            Directory.CreateDirectory(root);
            var emptyConfig = Path.Combine(root, "empty.gitconfig");
            File.WriteAllText(emptyConfig, string.Empty);
            using var process = Process.Start(new ProcessStartInfo(TestGitSpeed.Executable)
            {
                ArgumentList = { "init", "-q", "--ref-format=reftable", Path.Combine(root, "probe") },
                UseShellExecute = false,
                Environment = { ["GIT_CONFIG_GLOBAL"] = emptyConfig, ["GIT_CONFIG_NOSYSTEM"] = "1" },
            });
            if (!process!.WaitForExit(ProbeTimeout))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is IOException or Win32Exception)
        {
            return false;
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    /// <summary>Best effort removal of a temp directory that may hold a repository (read-only object files).</summary>
    public static void DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        // Object files are read-only, which blocks deletion on Windows. Elsewhere file names may not be valid UTF-8,
        // which .NET cannot address.
        if (OperatingSystem.IsWindows())
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of a temp directory.
        }
    }
}
