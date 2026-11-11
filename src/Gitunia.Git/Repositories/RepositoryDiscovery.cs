using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Resolves any path (root, subdirectory, linked worktree, bare repository, inside a git directory) to a <see cref="RepositoryLocation"/>.</summary>
public sealed class RepositoryDiscovery : IRepositoryDiscovery
{
    private static readonly string[] BasicsArguments = ["rev-parse", "--path-format=absolute", "--is-bare-repository", "--is-inside-git-dir", "--git-dir", "--git-common-dir", "--show-toplevel"];

    private const string NoWorkTreeMessage = "must be run in a work tree";
    private const int IsBareLine = 0;
    private const int IsInsideGitDirectoryLine = 1;
    private const int GitDirectoryLine = 2;
    private const int GitCommonDirectoryLine = 3;
    private const int TopLevelLine = 4;

    private static readonly string[] CoreWorktreeArguments = ["config", "--get", "core.worktree"];

    private readonly GitRunner _runner;

    public RepositoryDiscovery(GitRunner runner)
    {
        _runner = runner;
    }

    /// <exception cref="NotARepositoryException">The path does not exist or is outside any repository.</exception>
    public async Task<RepositoryLocation> DiscoverAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new NotARepositoryException(fullPath, "the directory does not exist");
        }

        // One call answers everything. The toplevel is asked last because rev-parse refuses it inside a git directory
        // and in bare repositories after printing the rest, which the failure still carries.
        string output;
        try
        {
            var basics = await _runner.RunAsync(
                fullPath,
                BasicsArguments,
                GitRunOptions.ReadOnlyRun,
                cancellationToken).ConfigureAwait(false);
            output = basics.StandardOutputText;
        }
        // The runner sets LC_ALL=C, so the message text is stable.
        catch (GitCommandFailedException exception) when (exception.StandardError.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotARepositoryException(fullPath, "no git repository found here or in any parent directory");
        }
        catch (GitCommandFailedException exception) when (exception.StandardError.Contains(NoWorkTreeMessage, StringComparison.Ordinal))
        {
            output = exception.StandardOutput;
        }

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= GitCommonDirectoryLine)
        {
            throw new GitException($"Unexpected rev-parse output for '{fullPath}'.");
        }

        var isBare = lines[IsBareLine].Trim() == "true";
        var isInsideGitDirectory = lines[IsInsideGitDirectoryLine].Trim() == "true";
        var gitDirectory = Path.GetFullPath(lines[GitDirectoryLine].Trim());
        var commonDirectory = Path.GetFullPath(lines[GitCommonDirectoryLine].Trim());
        var isLinkedWorktree = !string.Equals(gitDirectory, commonDirectory, StringComparison.Ordinal);

        if (isBare)
        {
            return new RepositoryLocation(null, gitDirectory, commonDirectory, true, false);
        }

        var topLevel = lines.Length > TopLevelLine ? lines[TopLevelLine].Trim() : string.Empty;
        var workingTree = topLevel.Length > 0 && !isInsideGitDirectory
            ? Path.GetFullPath(topLevel)
            : await ResolveWorkingTreeAsync(
                fullPath,
                gitDirectory,
                isInsideGitDirectory,
                isLinkedWorktree,
                cancellationToken).ConfigureAwait(false);

        return new RepositoryLocation(workingTree, gitDirectory, commonDirectory, false, isLinkedWorktree);
    }

    private async Task<string> ResolveWorkingTreeAsync(
        string fullPath,
        string gitDirectory,
        bool isInsideGitDirectory,
        bool isLinkedWorktree,
        CancellationToken cancellationToken)
    {
        if (isInsideGitDirectory)
        {
            return await ResolveWorkingTreeFromGitDirectoryAsync(
                fullPath,
                gitDirectory,
                isLinkedWorktree,
                cancellationToken).ConfigureAwait(false);
        }

        return await ReadTopLevelAsync(fullPath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ReadTopLevelAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            path,
            ["rev-parse", "--path-format=absolute", "--show-toplevel"],
            GitRunOptions.ReadOnlyRun,
            cancellationToken).ConfigureAwait(false);
        return Path.GetFullPath(result.StandardOutputText.Trim());
    }

    private async Task<string> ResolveWorkingTreeFromGitDirectoryAsync(
        string path,
        string gitDirectory,
        bool isLinkedWorktree,
        CancellationToken cancellationToken)
    {
        if (isLinkedWorktree)
        {
            // A linked worktree's git directory records the path of its '.git' file in 'gitdir'. The pointer can be
            // relative to that git directory (worktree.useRelativePaths), never to the process working directory.
            var pointer = (await File.ReadAllTextAsync(Path.Combine(gitDirectory, "gitdir"), cancellationToken).ConfigureAwait(false)).Trim();
            return Path.GetFullPath(Path.Combine(Path.GetFullPath(pointer, gitDirectory), ".."));
        }

        // The conventional layout is <working tree>/.git. Other layouts (separate git dir, submodules) set core.worktree.
        if (string.Equals(Path.GetFileName(gitDirectory), ".git", StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(gitDirectory, ".."));
        }

        var configured = await _runner.RunAsync(
            path,
            CoreWorktreeArguments,
            GitRunOptions.ReadOnlyRunAllowingNoMatch,
            cancellationToken).ConfigureAwait(false);
        var worktree = configured.StandardOutputText.Trim();
        if (worktree.Length == 0)
        {
            throw new NotARepositoryException(path, "the working tree of this git directory cannot be determined; open the working tree instead");
        }

        return Path.GetFullPath(worktree, gitDirectory);
    }
}
