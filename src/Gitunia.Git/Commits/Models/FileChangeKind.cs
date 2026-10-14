namespace Gitunia.Git.Commits.Models;

/// <summary>One side of the two-letter status code: what changed between HEAD and the index, or between the index and the working tree.</summary>
public enum FileChangeKind
{
    /// <summary>Shown as <c>.</c> by git.</summary>
    Unmodified,
    Modified,
    TypeChanged,
    Added,
    Deleted,
    Renamed,
    Copied,
    UpdatedButUnmerged,
}
