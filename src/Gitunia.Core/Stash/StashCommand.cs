using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Core.Stash;

/// <summary>A stash action or the result of one, for one tab. Dropped when the tab is no longer open (a late
/// result).</summary>
public abstract record StashCommand : AppCommand
{
    private protected StashCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Stash the changes: opens the message prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="IncludeUntracked">Also stash the untracked files.</param>
public sealed record StashPushRequested(TabId TabId, bool IncludeUntracked) : StashCommand(TabId);

/// <summary>Apply or pop the stash under the sidebar cursor.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The stash commit of the sidebar row the user saw, null when the row is no stash.</param>
/// <param name="Pop">Drop the stash after a clean apply.</param>
public sealed record StashApplyRequested(TabId TabId, string? CommitHash, bool Pop) : StashCommand(TabId);

/// <summary>Drop the stash under the sidebar cursor: asks for confirmation.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The stash commit of the sidebar row the user saw, null when the row is no stash.</param>
public sealed record StashDropRequested(TabId TabId, string? CommitHash) : StashCommand(TabId);

/// <summary>Restore the file of the shown stash under the cursor into the working tree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The file the user saw, null when the cursor is on no stash file.</param>
public sealed record StashRestoreFileRequested(TabId TabId, StashFileTarget? Target) : StashCommand(TabId);

/// <summary>The message prompt was accepted: stash.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="IncludeUntracked">Also stash the untracked files.</param>
/// <param name="Message">The typed message, empty for git's default.</param>
public sealed record StashMessageAccepted(TabId TabId, bool IncludeUntracked, string Message) : StashCommand(TabId);

/// <summary><c>y</c> in the drop confirm; the stash is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Entry">The stash.</param>
public sealed record StashDropConfirmed(TabId TabId, StashEntry Entry) : StashCommand(TabId);

/// <summary><c>y</c> in the restore confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The file.</param>
public sealed record StashRestoreConfirmed(TabId TabId, StashFileTarget Target) : StashCommand(TabId);

/// <summary>Result: a stash operation finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record StashOperationSucceeded(TabId TabId, string Text, StatusSeverity Severity)
    : StashCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Stash;
}

/// <summary>Result: a stash operation failed; the tab is refreshed anyway because git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record StashOperationFailed(TabId TabId, string Text) : StashCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Stash;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record StashOperationCancelled(TabId TabId) : StashCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Stash;
}

/// <summary>Result: what restoring the file would do was read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">The file.</param>
/// <param name="State">What the restore would do.</param>
public sealed record StashRestoreChecked(TabId TabId, StashFileTarget Target, StashRestoreState State)
    : StashCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Stash;
}
