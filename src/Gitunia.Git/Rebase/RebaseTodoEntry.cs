namespace Gitunia.Git.Rebase;

/// <summary>One line of the todo: the action for a commit. The order of the entries is the order of replay.</summary>
/// <param name="Commit">
/// The full hash of a commit of the range; for <see cref="RebaseTodoAction.Label"/> and
/// <see cref="RebaseTodoAction.Reset"/> the name they use.
/// </param>
/// <param name="Action">What to do with it.</param>
/// <param name="Message">
/// The new message for <see cref="RebaseTodoAction.Reword"/>, kept as typed; null otherwise.
/// </param>
/// <param name="Argument">
/// For <see cref="RebaseTodoAction.Merge"/> the rest of the line after the hash, as git wrote it (labels, then
/// <c># oneline</c>); null otherwise.
/// </param>
public sealed record RebaseTodoEntry(
    string Commit,
    RebaseTodoAction Action,
    string? Message = null,
    string? Argument = null);
