using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Worktrees;

/// <summary>A worktree action or the result of one, for one tab. Dropped when the tab is no longer open (a late
/// result).</summary>
public abstract record WorktreeCommand : AppCommand
{
    private protected WorktreeCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Add a worktree: opens the branch prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record WorktreeAddRequested(TabId TabId, WorktreeTarget Target) : WorktreeCommand(TabId);

/// <summary>Open the selected worktree as its own tab; on a local branch without a worktree, add one for it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record WorktreeOpenRequested(TabId TabId, WorktreeTarget Target) : WorktreeCommand(TabId);

/// <summary>Point the tab at the selected worktree, in place.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record WorktreeSwitchRequested(TabId TabId, WorktreeTarget Target) : WorktreeCommand(TabId);

/// <summary>Remove the selected worktree: asks for confirmation.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record WorktreeRemoveRequested(TabId TabId, WorktreeTarget Target) : WorktreeCommand(TabId);

/// <summary>Lock the selected worktree: opens the reason prompt.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record WorktreeLockRequested(TabId TabId, WorktreeTarget Target) : WorktreeCommand(TabId);

/// <summary>Unlock the selected worktree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What is selected.</param>
public sealed record WorktreeUnlockRequested(TabId TabId, WorktreeTarget Target) : WorktreeCommand(TabId);

/// <summary>Prune the worktree entries whose directory is gone: asks for confirmation.</summary>
/// <param name="TabId">The tab.</param>
public sealed record WorktreePruneRequested(TabId TabId) : WorktreeCommand(TabId);

/// <summary>The branch prompt of a new worktree was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="SelectedStart">The commit a new branch starts at, fixed when the prompt opened.</param>
/// <param name="Name">The typed branch: an existing one or a new name.</param>
public sealed record WorktreeBranchAccepted(TabId TabId, string? SelectedStart, string Name) : WorktreeCommand(TabId);

/// <summary>The path prompt of a new worktree was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The branch the worktree gets.</param>
/// <param name="StartPoint">The commit a new branch starts at, null for an existing branch.</param>
/// <param name="OpenAfter">True to open the new worktree as its own tab once it exists.</param>
/// <param name="Text">The typed directory.</param>
public sealed record WorktreePathAccepted(TabId TabId, string Branch, string? StartPoint, bool OpenAfter, string Text)
    : WorktreeCommand(TabId);

/// <summary>The lock reason prompt was accepted.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The worktree the prompt was opened for.</param>
/// <param name="Reason">The typed reason, empty for none.</param>
public sealed record WorktreeLockReasonAccepted(TabId TabId, string Path, string Reason) : WorktreeCommand(TabId);

/// <summary><c>y</c> in the remove confirm; the worktree is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The worktree.</param>
public sealed record WorktreeRemoveConfirmed(TabId TabId, string Path) : WorktreeCommand(TabId);

/// <summary><c>y</c> in the second confirm of a worktree that holds uncommitted changes.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The worktree.</param>
public sealed record WorktreeForceRemoveConfirmed(TabId TabId, string Path) : WorktreeCommand(TabId);

/// <summary><c>y</c> in the prune confirm.</summary>
/// <param name="TabId">The tab.</param>
public sealed record WorktreePruneConfirmed(TabId TabId) : WorktreeCommand(TabId);

/// <summary>Result: a worktree operation finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
/// <param name="OpenPath">The new worktree to open as its own tab, null to open none.</param>
public sealed record WorktreeOperationSucceeded(TabId TabId, string Text, string? OpenPath)
    : WorktreeCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Worktrees;
}

/// <summary>Result: a worktree operation failed; the tab is refreshed anyway, git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
public sealed record WorktreeOperationFailed(TabId TabId, string Text) : WorktreeCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Worktrees;
}

/// <summary>Result: the operation was cancelled (the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record WorktreeOperationCancelled(TabId TabId) : WorktreeCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Worktrees;
}

/// <summary>Result: the worktree holds uncommitted changes; ask a second time before deleting them.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The worktree.</param>
/// <param name="Changes">The changed and untracked paths.</param>
public sealed record WorktreeRemoveNeedsForce(TabId TabId, string Path, ImmutableArray<string> Changes)
    : WorktreeCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Worktrees;
}

/// <summary>Result: the path of a worktree was resolved to its location.</summary>
/// <param name="TabId">The tab the request came from.</param>
/// <param name="Location">The worktree's location.</param>
/// <param name="IsSwitch">True to point the tab at it, false to open it as its own tab.</param>
public sealed record WorktreeLocated(TabId TabId, RepositoryLocation Location, bool IsSwitch) : WorktreeCommand(TabId);

/// <summary>Result: the path of a worktree could not be resolved.</summary>
/// <param name="TabId">The tab the request came from.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record WorktreeLocateFailed(TabId TabId, string Text) : WorktreeCommand(TabId);

/// <summary>The status of the tab's other worktrees was read for the sidebar overview.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the tab was when the read started; a tab that moved meanwhile ignores the
/// result.</param>
/// <param name="Statuses">The status of each worktree by path.</param>
public sealed record WorktreeStatusesRead(
    TabId TabId,
    RepositoryLocation Location,
    ImmutableDictionary<string, WorktreeStatus> Statuses) : WorktreeCommand(TabId);
