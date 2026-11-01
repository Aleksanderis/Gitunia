using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>A command on the interactive rebase editor or the result of one of its effects, for one tab.</summary>
public abstract record RebaseEditorCommand : AppCommand
{
    private protected RebaseEditorCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for. Dropped when the tab is gone.</summary>
    public TabId TabId { get; }
}

/// <summary>A result of a read (the range or a message): it ends the read and the running operation.</summary>
public abstract record RebaseEditorReadResult : RebaseEditorCommand, IOperationResult
{
    private protected RebaseEditorReadResult(TabId tabId)
        : base(tabId)
    {
    }

    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Open the editor for the current branch: onto the selection, or over the whole history.</summary>
/// <param name="TabId">The active tab.</param>
/// <param name="Selection">What is selected.</param>
/// <param name="IsRoot">True to rebase the whole history, whatever is selected.</param>
public sealed record RebaseEditorOpenRequested(TabId TabId, RebaseSelection Selection, bool IsRoot)
    : RebaseEditorCommand(TabId);

/// <summary>A bound action while the editor is open (it consumes every action).</summary>
/// <param name="TabId">The tab of the editor.</param>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="PageRows">How many commit rows the list shows, for page motions.</param>
public sealed record RebaseEditorActionPressed(TabId TabId, ActionId Action, int? Count, int PageRows)
    : RebaseEditorCommand(TabId);

/// <summary>A key typed into the message editor of a reword.</summary>
/// <param name="TabId">The tab of the editor.</param>
/// <param name="Key">The key.</param>
public sealed record RebaseEditorKeyTyped(TabId TabId, KeyEvent Key) : RebaseEditorCommand(TabId);

/// <summary>A click on a commit row.</summary>
/// <param name="TabId">The tab of the editor.</param>
/// <param name="Index">The row.</param>
public sealed record RebaseEditorRowClicked(TabId TabId, int Index) : RebaseEditorCommand(TabId);

/// <summary><c>y</c> in the confirm; the todo is what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="State">The todo as confirmed.</param>
public sealed record RebaseEditorStartConfirmed(TabId TabId, RebaseEditorState State) : RebaseEditorCommand(TabId);

/// <summary>Result: the range was read and the todo built.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The read number of the effect.</param>
/// <param name="State">The fresh todo, every commit a pick.</param>
/// <param name="TargetHash">The commit rebased onto, null for the whole history.</param>
public sealed record RebaseEditorRangeRead(TabId TabId, long Generation, RebaseEditorState State, string? TargetHash)
    : RebaseEditorReadResult(TabId);

/// <summary>Result: the message of the commit to reword was read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The read number of the effect.</param>
/// <param name="Hash">The commit.</param>
/// <param name="Text">The full message, or the subject when git could not deliver it.</param>
public sealed record RebaseEditorMessageRead(TabId TabId, long Generation, string Hash, string Text)
    : RebaseEditorReadResult(TabId);

/// <summary>Result: the range could not be read; nothing opens.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How loud to tell it.</param>
public sealed record RebaseEditorReadFailed(TabId TabId, string Text, StatusSeverity Severity)
    : RebaseEditorReadResult(TabId);

/// <summary>Result: a read was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record RebaseEditorReadCancelled(TabId TabId) : RebaseEditorReadResult(TabId);

/// <summary>Result: the rebase ran; it finished or stopped on conflicts.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="State">The todo that was run, for the report.</param>
/// <param name="Result">What the engine did.</param>
public sealed record RebaseEditorFinished(TabId TabId, RebaseEditorState State, InteractiveRebaseResult Result)
    : RebaseEditorCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}

/// <summary>Result: nothing ran (the engine could not start); the todo comes back so editing it was not lost.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="State">The todo that was handed over.</param>
/// <param name="Text">Why it could not start.</param>
public sealed record RebaseEditorNotStarted(TabId TabId, RebaseEditorState State, string Text)
    : RebaseEditorCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Rebase;
}
