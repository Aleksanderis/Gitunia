namespace Gitunia.Git.Rebase;

/// <summary>A commit of the range an interactive rebase can edit.</summary>
/// <param name="Hash">The full hash.</param>
/// <param name="Subject">The first line of its message.</param>
public sealed record RebaseTodoCommit(string Hash, string Subject);
