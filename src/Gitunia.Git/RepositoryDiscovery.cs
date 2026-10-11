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
            ? ResolveWorkingTreeFromGitDirectory(gitDirectory, commonDirectory, isLinkedWorktree)
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

    private static string ResolveWorkingTreeFromGitDirectory(string gitDirectory, string commonDirectory, bool isLinkedWorktree)
    {
        if (!isLinkedWorktree)
        {
            return Path.GetFullPath(Path.Combine(gitDirectory, ".."));
        }

        // A linked worktree's git directory records the path of its '.git' file in 'gitdir'.
        string pointerFile = Path.Combine(gitDirectory, "gitdir");
        string pointer = File.ReadAllText(pointerFile).Trim();
        return Path.GetFullPath(Path.Combine(Path.GetFullPath(pointer), ".."));
    }
}
