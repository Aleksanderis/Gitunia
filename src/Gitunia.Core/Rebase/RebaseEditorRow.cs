using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>One line of the todo being edited: a commit of the range and what the rebase does with it.</summary>
/// <param name="Hash">The full hash.</param>
/// <param name="Subject">The first line of the commit's current message.</param>
/// <param name="Action">What the rebase does with the commit.</param>
/// <param name="Message">
/// The edited message. Kept when the action changes away from reword, so choosing reword again brings it back; only a
/// reword sends it to git. Null while the message was never edited.
/// </param>
public sealed record RebaseEditorRow(string Hash, string Subject, RebaseTodoAction Action, string? Message = null)
{
    /// <summary>The subject the rebase will give the commit: the edited message's first line for a reword.</summary>
    public string ShownSubject =>
        Action == RebaseTodoAction.Reword && Message is { } message ? FirstLine(message) : Subject;

    private static string FirstLine(string message)
    {
        var end = message.IndexOf('\n', StringComparison.Ordinal);
        return end < 0 ? message : message[..end];
    }
}
