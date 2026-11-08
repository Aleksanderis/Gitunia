using Gitunia.Core.Keymap;

namespace Gitunia.Core.Status;

/// <summary>
/// The plain-words undo lines of the confirm dialogs. The exact restore command and the full hash are never shown
/// there: they go to the detail of the status message after the action, which the message history shows.
/// </summary>
public static class UndoNotes
{
    /// <summary>The message history and its key; the token is resolved where the keymap is known (#1196).</summary>
    public static readonly string MessageHistory = KeyTokens.Of(ActionIds.StatusHistory, "the message history");

    /// <summary>The reflog and its key; the token is resolved where the keymap is known (#1196).</summary>
    public static readonly string Reflog = KeyTokens.Of(ActionIds.ReflogOpen, "the reflog");

    /// <summary>For actions that git can undo with a command we know: the message history will hold it.</summary>
    public static readonly string FromHistory = $"Afterwards {MessageHistory} shows how to bring it back.";

    /// <summary>For actions that move HEAD: the reflog also holds the old position.</summary>
    public static readonly string FromReflog =
        $"Afterwards you can go back from {Reflog}; {MessageHistory} has the exact command.";

    /// <summary>The detail line of a status message: what to run, in the message history.</summary>
    public static string Restore(string command) => $"Restore: {command}";

    /// <summary>The detail line of a status message that undoes a move: the command and where it came from.</summary>
    public static string Undo(string command) => $"Undo: {command}";
}
