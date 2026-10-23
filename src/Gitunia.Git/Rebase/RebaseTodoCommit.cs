namespace Gitunia.Git.Rebase;

/// <summary>
/// A line of the range an interactive rebase can edit: a commit, or with <c>--rebase-merges</c> also a merge commit
/// and the <c>label</c> and <c>reset</c> lines that tie the branches together.
/// </summary>
/// <param name="Hash">The full hash; for <c>label</c> and <c>reset</c> the name they use.</param>
/// <param name="Subject">The first line of its message; empty for <c>label</c> and <c>reset</c>.</param>
/// <param name="Action">
/// <see cref="RebaseTodoAction.Pick"/> for a commit, <see cref="RebaseTodoAction.Merge"/> for a merge commit,
/// <see cref="RebaseTodoAction.Label"/> or <see cref="RebaseTodoAction.Reset"/> for the structure lines.
/// </param>
/// <param name="Argument">
/// For a merge, the rest of its line after the hash (the labels and the oneline); else null.
/// </param>
public sealed record RebaseTodoCommit(
    string Hash,
    string Subject,
    RebaseTodoAction Action = RebaseTodoAction.Pick,
    string? Argument = null);
