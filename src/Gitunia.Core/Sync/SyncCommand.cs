using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Sync;

/// <summary>A fetch, pull, push or remote branch delete action, or the result of one, for one tab. Dropped when the tab
/// is no longer open (a late result or answer).</summary>
public abstract record SyncCommand : AppCommand
{
    private protected SyncCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>A key asked for a fetch, pull or push.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Kind">What was asked for.</param>
public sealed record SyncRequested(TabId TabId, SyncKind Kind) : SyncCommand(TabId);

/// <summary>Stop the running fetch, pull or push (or say that none runs).</summary>
/// <param name="TabId">The tab.</param>
public sealed record SyncCancelRequested(TabId TabId) : SyncCommand(TabId);

/// <summary>Delete a branch on a remote: reads the remotes first.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Target">What the cursor is on.</param>
public sealed record SyncDeleteRequested(TabId TabId, SyncTarget Target) : SyncCommand(TabId);

/// <summary>The remote of a first push was chosen in the picker.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The branch the prompt was opened for.</param>
/// <param name="Remote">The chosen remote.</param>
public sealed record SyncRemoteChosen(TabId TabId, string Branch, string Remote) : SyncCommand(TabId);

/// <summary>The remote to delete a branch from was chosen in the picker: ask for the branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branches">The remote branches known when the action started.</param>
/// <param name="Remote">The chosen remote.</param>
public sealed record SyncDeleteRemoteChosen(TabId TabId, ImmutableArray<RemoteBranchRef> Branches, string Remote)
    : SyncCommand(TabId);

/// <summary>The branch to delete was chosen in the picker: ask to confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branches">The remote branches known when the action started.</param>
/// <param name="Remote">The remote chosen before.</param>
/// <param name="Branch">The chosen branch.</param>
public sealed record SyncDeleteBranchChosen(
    TabId TabId,
    ImmutableArray<RemoteBranchRef> Branches,
    string Remote,
    string Branch) : SyncCommand(TabId);

/// <summary><c>y</c> in the remote branch delete confirm; the branch is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Remote">The remote.</param>
/// <param name="Branch">The branch on it.</param>
/// <param name="CommitHash">The tip the user saw, the lease of the delete.</param>
public sealed record SyncDeleteConfirmed(TabId TabId, string Remote, string Branch, string CommitHash)
    : SyncCommand(TabId);

/// <summary><c>y</c> in the force push confirm; the push is named by what the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Request">What to push, with the lease.</param>
/// <param name="Done">The status text of a success.</param>
public sealed record SyncForceConfirmed(TabId TabId, PushRequest Request, string Done) : SyncCommand(TabId);

/// <summary>Progress: git reported a stage of the running transfer.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Report">What git reported.</param>
public sealed record SyncProgressed(TabId TabId, RemoteProgress Report) : SyncCommand(TabId);

/// <summary>Result: a transfer or a delete finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record SyncSucceeded(TabId TabId, string Text) : SyncCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Sync;
}

/// <summary>Result: a transfer or a delete failed; the tab is refreshed anyway because git may have done part of it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">The first error line with what was tried.</param>
/// <param name="Retry">The terminal retry to offer, null when the failure was not about credentials.</param>
/// <param name="Rejection">Set when a push was rejected as a non-fast-forward.</param>
public sealed record SyncFailed(TabId TabId, string Text, SyncRetry? Retry, SyncRejection? Rejection)
    : SyncCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Sync;
}

/// <summary>Result: the operation was cancelled (by the user, or the tab was closed).</summary>
/// <param name="TabId">The tab.</param>
public sealed record SyncCancelled(TabId TabId) : SyncCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Sync;
}

/// <summary>Result: the remotes were read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Purpose">Why they were read.</param>
/// <param name="Branch">The branch of a first push or a force push, null for a delete.</param>
/// <param name="DirectName">The one remote branch the cursor named for a delete, null when none.</param>
/// <param name="Remotes">The remote names.</param>
public sealed record SyncRemotesListed(
    TabId TabId,
    SyncRemotesPurpose Purpose,
    string? Branch,
    string? DirectName,
    ImmutableArray<string> Remotes) : SyncCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Sync;
}

/// <summary>Result: the check whether the local branch is behind the remote one finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Branch">The branch that was pushed.</param>
/// <param name="CommitHash">The local tip that was compared.</param>
/// <param name="Remote">The remote it was pushed to.</param>
/// <param name="TrackingHash">The tip of the remote-tracking branch that was compared.</param>
/// <param name="IsBehind">The local tip is in the history of the remote one, so a force would lose nothing of ours
/// and a pull or rebase is the answer.</param>
public sealed record SyncBehindChecked(
    TabId TabId,
    string Branch,
    string CommitHash,
    string Remote,
    string TrackingHash,
    bool IsBehind) : SyncCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Sync;
}

/// <summary>Result: the remotes could not be read or the branches not compared.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record SyncListFailed(TabId TabId, string Text) : SyncCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Sync;
}
