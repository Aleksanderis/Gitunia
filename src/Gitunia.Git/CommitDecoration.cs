namespace Gitunia.Git;

/// <summary>A ref that points at a commit, as shown next to it in the graph.</summary>
/// <param name="FullName">For example <c>refs/heads/main</c>, or <c>HEAD</c> for a detached HEAD.</param>
/// <param name="Name">Short name, for example <c>main</c> or <c>origin/main</c>.</param>
/// <param name="Kind">Local branch, remote branch, tag, detached HEAD or other.</param>
/// <param name="IsCurrent">HEAD is on this branch.</param>
public sealed record CommitDecoration(string FullName, string Name, RefKind Kind, bool IsCurrent);
