namespace Gitunia.Git.Blame.Models;

/// <summary>The commit a blamed line comes from, as <c>git blame --incremental</c> describes it.</summary>
/// <param name="Id">The full commit id; all zeros for lines that are not committed yet.</param>
/// <param name="Author">The author's name.</param>
/// <param name="AuthorTime">When the author made the commit.</param>
/// <param name="Summary">The commit subject.</param>
/// <param name="IsBoundary">True for a commit at the end of the walked history (git marks it with a caret).</param>
/// <param name="Path">The file's path in that commit (differs from the asked path after a rename).</param>
public sealed record BlameCommit(
    string Id,
    string Author,
    DateTimeOffset AuthorTime,
    string Summary,
    bool IsBoundary,
    string Path)
{
    /// <summary>The id git prints for lines of the working tree that are in no commit.</summary>
    public const string UncommittedId = "0000000000000000000000000000000000000000";

    /// <summary>True for a line that is in the working tree only.</summary>
    public bool IsUncommitted => Id == UncommittedId;
}
