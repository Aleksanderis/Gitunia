namespace Gitunia.Git.Diff.Models;

/// <summary>How a file differs between the two sides of a diff.</summary>
public enum DiffFileStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
}
