namespace Gitunia.Core.Status;

/// <summary>
/// The plain-words undo lines of the confirm dialogs. The exact restore command and the full hash are never shown
/// there: they go to the detail of the status message after the action, which the message history shows.
/// </summary>
public static class UndoNotes
{
    /// <summary>The default key of the message history; messages that name a key keep naming it.</summary>
    public const string MessagesKey = "<space>m";

    /// <summary>The default key of the reflog.</summary>
    public const string ReflogKey = "<space>l";

    /// <summary>For actions that git can undo with a command we know: the message history will hold it.</summary>
    public const string FromHistory = $"Afterwards the message history ({MessagesKey}) shows how to bring it back.";

    /// <summary>For actions that move HEAD: the reflog also holds the old position.</summary>
    public const string FromReflog =
        $"Afterwards you can go back from the reflog ({ReflogKey}); the message history ({MessagesKey}) has the exact command.";

    /// <summary>The detail line of a status message: what to run, in the message history.</summary>
    public static string Restore(string command) => $"Restore: {command}";

    /// <summary>The detail line of a status message that undoes a move: the command and where it came from.</summary>
    public static string Undo(string command) => $"Undo: {command}";
}
