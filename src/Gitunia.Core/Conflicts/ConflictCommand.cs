using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>A command on the conflict editor: user intent and the results of its effects.</summary>
public abstract record ConflictCommand : AppCommand;

/// <summary>
/// A command from the terminal layer. It carries the pane rows measured for the current screen, because the editor
/// scrolls its panes to the cursor and only the terminal layer knows how many rows the chips leave.
/// </summary>
/// <param name="Viewports">The pane rows, null when the screen is too small to draw the editor.</param>
public abstract record ConflictInputCommand(ConflictViewports? Viewports) : ConflictCommand;

/// <summary>A command or result that belongs to one tab. A result for a tab that closed meanwhile only
/// reports.</summary>
public abstract record ConflictTabCommand : ConflictCommand
{
    private protected ConflictTabCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Open the editor on the file under the Unstaged cursor, or on the first conflicted one.</summary>
/// <param name="FromOpenKey">True for the editor key (any pane, the first conflicted file when none is under the
/// cursor); false for Enter, which opens only the file under the Unstaged cursor.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictOpenRequested(bool FromOpenKey, ConflictViewports? Viewports)
    : ConflictInputCommand(Viewports);

/// <summary>A bound action while the editor is open (it consumes every action).</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictActionPressed(ActionId Action, int? Count, ConflictViewports? Viewports)
    : ConflictInputCommand(Viewports);

/// <summary>A key typed while the result is in Insert mode.</summary>
/// <param name="Key">The key.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictKeyTyped(KeyEvent Key, ConflictViewports? Viewports) : ConflictInputCommand(Viewports);

/// <summary>A click on a header or a row (a chip is a mouse view and never reaches the reducer).</summary>
/// <param name="Target">What was hit.</param>
/// <param name="IsDoubleClick">True for the second click of a double-click.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictClicked(ConflictTarget Target, bool IsDoubleClick, ConflictViewports? Viewports)
    : ConflictInputCommand(Viewports);

/// <summary>A drag began on a row of a version pane.</summary>
/// <param name="Pane">The pane.</param>
/// <param name="Row">The aligned row.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictDragStarted(ConflictPane Pane, int Row, ConflictViewports? Viewports)
    : ConflictInputCommand(Viewports);

/// <summary>The pointer of a drag reached a row of a version pane.</summary>
/// <param name="Row">The aligned row.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictDragged(int Row, ConflictViewports? Viewports) : ConflictInputCommand(Viewports);

/// <summary>The pointer of a drag started in the result reached a text position: the selection reaches it.</summary>
/// <param name="Line">The result line.</param>
/// <param name="Column">The display column in the line.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictResultDragged(int Line, int Column, ConflictViewports? Viewports)
    : ConflictInputCommand(Viewports);

/// <summary>A terminal paste while the result is in Normal mode (while typing, a paste is typed as keys).</summary>
/// <param name="Text">The pasted text.</param>
/// <param name="Viewports">The pane rows.</param>
public sealed record ConflictPasted(string Text, ConflictViewports? Viewports) : ConflictInputCommand(Viewports);

/// <summary>The tab of the open editor was closed: the editor closes.</summary>
public sealed record ConflictTabClosed : ConflictCommand;

/// <summary>The save question was answered yes: write the result as it is now.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The editor the question was asked for.</param>
public sealed record ConflictSaveConfirmed(TabId TabId, long Generation) : ConflictTabCommand(TabId);

/// <summary>The re-create question was answered yes.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The editor the question was asked for.</param>
public sealed record ConflictRecreateConfirmed(TabId TabId, long Generation) : ConflictTabCommand(TabId);

/// <summary>Result: the file was read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The open number of the read effect.</param>
/// <param name="Path">The file.</param>
/// <param name="Conflict">The kind of conflict the status reported.</param>
/// <param name="Result">The parts, or why the editor cannot take the file.</param>
public sealed record ConflictFileRead(
    TabId TabId,
    long Generation,
    string Path,
    ConflictKind Conflict,
    ConflictReadResult Result) : ConflictTabCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Conflicts;
}

/// <summary>Result: the result was written to the working tree file.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The file.</param>
/// <param name="Message">What to tell the user.</param>
/// <param name="IsComplete">True when no conflict is left, so the file is marked resolved.</param>
public sealed record ConflictSaved(TabId TabId, string Path, string Message, bool IsComplete)
    : ConflictTabCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Conflicts;
}

/// <summary>Result: the conflict of the file was re-created and the file read again.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The file.</param>
/// <param name="Result">The parts of the new conflict, or why the editor cannot take the file.</param>
public sealed record ConflictRecreated(TabId TabId, string Path, ConflictReadResult Result)
    : ConflictTabCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Conflicts;
}

/// <summary>Result: a read, save or re-create failed or was refused; the editor stays as it is.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How it is drawn.</param>
public sealed record ConflictOperationFailed(TabId TabId, string Text, StatusSeverity Severity)
    : ConflictTabCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Conflicts;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record ConflictOperationCancelled(TabId TabId) : ConflictTabCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Conflicts;
}
