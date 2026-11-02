using Gitunia.Git.Commits.Models;

namespace Gitunia.Git.History.Models;

/// <summary>One commit of a file's history, newest first.</summary>
/// <param name="Hash">The full commit hash.</param>
/// <param name="AuthorName">The author's name.</param>
/// <param name="Date">The author date, short form (<c>2026-11-02</c>).</param>
/// <param name="Subject">The first line of the message.</param>
/// <param name="Path">The path the file had after this commit (before it, for a deletion).</param>
/// <param name="OldPath">The path before a rename or copy, null otherwise.</param>
/// <param name="Kind">What the commit did to the file.</param>
public sealed record FileHistoryEntry(
    string Hash,
    string AuthorName,
    string Date,
    string Subject,
    string Path,
    string? OldPath,
    FileChangeKind Kind);
