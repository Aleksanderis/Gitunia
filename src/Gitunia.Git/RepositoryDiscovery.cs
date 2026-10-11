namespace Gitunia.Git;

/// <summary>Resolves any path (root, subdirectory, linked worktree, bare repository, inside a git directory) to a <see cref="RepositoryLocation"/>.</summary>
public static class RepositoryDiscovery
{
    /// <exception cref="NotARepositoryException">The path does not exist or is outside any repository.</exception>
    public static async Task<RepositoryLocation> DiscoverAsync(GitRunner runner, string path, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new NotARepositoryException(fullPath, "the directory does not exist");
        }

        // One call answers everything except the toplevel, which fails inside a git directory and in bare repositories.
        GitResult basics;
        try
        {
            basics = await runner.RunAsync(
                fullPath,
                ["rev-parse", "--path-format=absolute", "--is-bare-repository", "--is-inside-git-dir", "--git-dir", "--git-common-dir"],
                new GitRunOptions(ReadOnly: true),
                cancellationToken).ConfigureAwait(false);
        }
        // The runner sets LC_ALL=C, so the message text is stable.
        catch (GitCommandFailedException exception) when (exception.StandardError.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotARepositoryException(fullPath, "no git repository found here or in any parent directory");
        }

        string[] lines = basics.StandardOutputText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 4)
        {
            throw new GitException($"Unexpected rev-parse output for '{fullPath}'.");
        }

        bool isBare = lines[0].Trim() == "true";
        bool isInsideGitDirectory = lines[1].Trim() == "true";
        string gitDirectory = Path.GetFullPath(lines[2].Trim());
        string commonDirectory = Path.GetFullPath(lines[3].Trim());
        bool isLinkedWorktree = !string.Equals(gitDirectory, commonDirectory, StringComparison.Ordinal);

        if (isBare)
        {
            return new RepositoryLocation(null, gitDirectory, commonDirectory, true, false);
        }

        string workingTree = isInsideGitDirectory
            ? await ResolveWorkingTreeFromGitDirectoryAsync(runner, fullPath, gitDirectory, isLinkedWorktree, cancellationToken).ConfigureAwait(false)
            : await ReadTopLevelAsync(runner, fullPath, cancellationToken).ConfigureAwait(false);
        return new RepositoryLocation(workingTree, gitDirectory, commonDirectory, false, isLinkedWorktree);
    }

    private static async Task<string> ReadTopLevelAsync(GitRunner runner, string path, CancellationToken cancellationToken)
    {
        GitResult result = await runner.RunAsync(
            path,
            ["rev-parse", "--path-format=absolute", "--show-toplevel"],
            new GitRunOptions(ReadOnly: true),
            cancellationToken).ConfigureAwait(false);
        return Path.GetFullPath(result.StandardOutputText.Trim());
    }

    private static async Task<string> ResolveWorkingTreeFromGitDirectoryAsync(
        GitRunner runner,
        string path,
        string gitDirectory,
        bool isLinkedWorktree,
        CancellationToken cancellationToken)
    {
        if (isLinkedWorktree)
        {
            // A linked worktree's git directory records the path of its '.git' file in 'gitdir'. The pointer can be
            // relative to that git directory (worktree.useRelativePaths), never to the process working directory.
            string pointer = (await File.ReadAllTextAsync(Path.Combine(gitDirectory, "gitdir"), cancellationToken).ConfigureAwait(false)).Trim();
            return Path.GetFullPath(Path.Combine(Path.GetFullPath(pointer, gitDirectory), ".."));
        }

        // The conventional layout is <working tree>/.git. Other layouts (separate git dir, submodules) set core.worktree.
        if (string.Equals(Path.GetFileName(gitDirectory), ".git", StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(gitDirectory, ".."));
        }

        GitResult configured = await runner.RunAsync(
            path,
            ["config", "--get", "core.worktree"],
            new GitRunOptions(ReadOnly: true, AcceptedExitCodes: [1]),
            cancellationToken).ConfigureAwait(false);
        string worktree = configured.StandardOutputText.Trim();
        if (worktree.Length == 0)
        {
            throw new NotARepositoryException(path, "the working tree of this git directory cannot be determined; open the working tree instead");
        }

        return Path.GetFullPath(worktree, gitDirectory);
    }
}
