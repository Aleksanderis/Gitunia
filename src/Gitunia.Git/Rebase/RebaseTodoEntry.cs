namespace Gitunia.Git.Rebase;

/// <summary>One line of the todo: the action for a commit. The order of the entries is the order of replay.</summary>
/// <param name="Commit">The full hash of a commit of the range.</param>
/// <param name="Action">What to do with it.</param>
/// <param name="Message">
/// The new message for <see cref="RebaseTodoAction.Reword"/>, kept as typed; null otherwise.
/// </param>
public sealed record RebaseTodoEntry(string Commit, RebaseTodoAction Action, string? Message = null);
