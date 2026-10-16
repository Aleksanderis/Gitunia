namespace Gitunia.Git.Commits.Models;

/// <summary>The outcome of a commit.</summary>
/// <param name="Hash">Full hash of the new HEAD.</param>
/// <param name="PreviousHash">Full hash HEAD had before an amend, so the user can go back to it; null for a plain commit.</param>
public sealed record CommitResult(string Hash, string? PreviousHash);
