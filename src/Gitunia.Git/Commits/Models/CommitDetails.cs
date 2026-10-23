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
    /// <summary>
    /// Index in <see cref="Parents"/> of the parent <see cref="Files"/> are against: 0 (the first parent) unless
    /// switched a merge commit to another parent.
    /// </summary>
    public int ParentIndex { get; init; }

    /// <summary>
    /// The older commit of a comparison (#535): <see cref="Files"/> are what <c>git diff older newer</c> changes, and
    /// <see cref="Id"/> is the commit selected in the graph, which is either end. Null for an ordinary commit.
    /// </summary>
    public ObjectId? CompareOlder { get; init; }

    /// <summary>The newer commit of a comparison, null for an ordinary commit.</summary>
    public ObjectId? CompareNewer { get; init; }

    /// <summary>These details show the difference between two commits and not one commit's own changes.</summary>
    public bool IsComparison => CompareOlder is not null && CompareNewer is not null;

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
