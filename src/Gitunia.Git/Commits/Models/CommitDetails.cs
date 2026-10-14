namespace Gitunia.Git.Commits.Models;

/// <summary>Everything the detail pane shows about one commit.</summary>
/// <param name="Id">The commit id.</param>
/// <param name="Parents">Parents, first parent first.</param>
/// <param name="AuthorName">Author name.</param>
/// <param name="AuthorEmail">Author email.</param>
/// <param name="AuthorTime">Author time with its original UTC offset.</param>
/// <param name="CommitterName">Committer name.</param>
/// <param name="CommitterEmail">Committer email.</param>
/// <param name="CommitterTime">Commit time with its original UTC offset.</param>
/// <param name="Message">The full message (subject, blank line, body) without the trailing newline.</param>
/// <param name="Files">Files changed against the first parent.</param>
public sealed record CommitDetails(
    ObjectId Id,
    IReadOnlyList<ObjectId> Parents,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorTime,
    string CommitterName,
    string CommitterEmail,
    DateTimeOffset CommitterTime,
    string Message,
    IReadOnlyList<CommitFileChange> Files)
{
    /// <summary>The first line of the message.</summary>
    public string Subject
    {
        get
        {
            var end = Message.IndexOf('\n', StringComparison.Ordinal);
            return end < 0 ? Message : Message[..end];
        }
    }

    /// <summary>The author and committer differ, so both are worth showing.</summary>
    public bool HasDistinctCommitter => AuthorName != CommitterName || AuthorEmail != CommitterEmail;
}
