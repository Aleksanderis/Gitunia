namespace Gitunia.Git.Watching;

/// <summary>
/// Lists the working tree directories worth watching: the ones that hold tracked or non-ignored files.
/// </summary>
public interface IWorkingTreeDirectoryLister
{
    /// <summary>
    /// Absolute paths of the directories below <paramref name="workingTreePath"/> (the root itself excluded)
    /// that contain at least one tracked or untracked-but-not-ignored file. Null when git could not tell.
    /// </summary>
    Task<IReadOnlyList<string>?> ListAsync(string workingTreePath, CancellationToken cancellationToken);

    /// <summary>
    /// Absolute paths of the ignored directories below <paramref name="workingTreePath"/> (git's <c>--directory</c> view:
    /// an ignored directory is listed once, not its files). Null when git could not tell.
    /// </summary>
    Task<IReadOnlyList<string>?> ListIgnoredAsync(string workingTreePath, CancellationToken cancellationToken);
}
