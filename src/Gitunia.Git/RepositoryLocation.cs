namespace Gitunia.Git;

/// <summary>Where a repository lives on disk, resolved from any path inside it. All paths are absolute.</summary>
/// <param name="WorkingTreePath">The working tree root, or null for a bare repository.</param>
/// <param name="GitDirectory">The git directory of this working tree (for a linked worktree, inside the common directory).</param>
/// <param name="CommonDirectory">The git directory shared by all worktrees.</param>
/// <param name="IsBare">Whether the repository has no working tree.</param>
/// <param name="IsLinkedWorktree">Whether this is a linked worktree rather than the main working tree.</param>
public sealed record RepositoryLocation(
    string? WorkingTreePath,
    string GitDirectory,
    string CommonDirectory,
    bool IsBare,
    bool IsLinkedWorktree)
{
    /// <summary>The identity used to dedupe tabs: the working tree root, or the git directory for bare repositories.</summary>
    public string RootPath => WorkingTreePath ?? GitDirectory;

    /// <summary>The path git is run against.</summary>
    public string CommandPath => WorkingTreePath ?? GitDirectory;
}
