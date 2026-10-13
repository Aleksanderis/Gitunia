namespace Gitunia.Git;

/// <summary>A branch or tag.</summary>
/// <param name="FullName">For example <c>refs/heads/main</c>.</param>
/// <param name="Name">Short name, for example <c>main</c> or <c>origin/main</c>.</param>
/// <param name="Kind">Local branch, remote branch or tag.</param>
/// <param name="ObjectHash">What the ref points at (the tag object for annotated tags).</param>
/// <param name="CommitHash">The commit: for annotated tags the peeled commit, otherwise the same as <paramref name="ObjectHash"/>.</param>
/// <param name="UpstreamFullName">Full name of the configured upstream (local branches), or null.</param>
/// <param name="Ahead">Commits ahead of the upstream, null when there is no upstream or it is gone.</param>
/// <param name="Behind">Commits behind the upstream, null when there is no upstream or it is gone.</param>
/// <param name="IsUpstreamGone">The configured upstream no longer exists.</param>
/// <param name="IsCurrent">HEAD of this worktree is on the branch.</param>
/// <param name="CheckedOutIn">Path of the worktree that has the branch checked out, or null.</param>
/// <param name="SymbolicTarget">For symbolic refs such as <c>refs/remotes/origin/HEAD</c>, the full name they point to.</param>
public sealed record GitRef(
    string FullName,
    string Name,
    RefKind Kind,
    string ObjectHash,
    string CommitHash,
    string? UpstreamFullName,
    int? Ahead,
    int? Behind,
    bool IsUpstreamGone,
    bool IsCurrent,
    string? CheckedOutIn,
    string? SymbolicTarget);
