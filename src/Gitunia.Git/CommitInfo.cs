namespace Gitunia.Git;

/// <summary>A commit as shown in the graph. Parents are ordered, first parent first.</summary>
public sealed record CommitInfo(
    ObjectId Id,
    IReadOnlyList<ObjectId> Parents,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorTime,
    string Subject,
    IReadOnlyList<CommitDecoration> Decorations);
