namespace Gitunia.Git.Worktrees;

/// <summary>One <c>key value</c> line of a worktree porcelain record.</summary>
/// <param name="Key">The attribute name, for example <c>worktree</c> or <c>HEAD</c>.</param>
/// <param name="Value">The rest of the line, empty for flag attributes such as <c>bare</c>.</param>
public sealed record WorktreeProperty(string Key, string Value);
