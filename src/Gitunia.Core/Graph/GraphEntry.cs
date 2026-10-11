using Gitunia.Git;

namespace Gitunia.Core.Graph;

/// <summary>
/// What the graph view needs of one commit: a compact copy of <see cref="CommitInfo"/> (no parent list, no email)
/// together with its lane layout. Details of a selected commit are loaded on demand, not kept per row.
/// </summary>
/// <param name="Id">The commit id.</param>
/// <param name="Subject">First line of the message.</param>
/// <param name="AuthorName">Author name.</param>
/// <param name="AuthorTime">Author time.</param>
/// <param name="Decorations">Refs pointing at the commit.</param>
/// <param name="Row">Lane layout of the commit.</param>
public sealed record GraphEntry(
    ObjectId Id,
    string Subject,
    string AuthorName,
    DateTimeOffset AuthorTime,
    IReadOnlyList<CommitDecoration> Decorations,
    GraphRow Row);
