namespace Gitunia.Core.Status;

/// <summary>
/// The detail lines of the status messages after an undoable action. The exact restore command and the full hash go
/// there, to the message history; the confirm dialogs carry no restore or undo note (#1480).
/// </summary>
public static class UndoNotes
{
    /// <summary>The detail line of a status message: what to run, in the message history.</summary>
    public static string Restore(string command) => $"Restore: {command}";

    /// <summary>The detail line of a status message that undoes a move: the command and where it came from.</summary>
    public static string Undo(string command) => $"Undo: {command}";
}
