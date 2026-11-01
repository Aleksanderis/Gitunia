using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Sync;

/// <summary>An operation on a remote. Each posts one result <see cref="SyncCommand"/>; none changes state itself.</summary>
public abstract record SyncEffect : OperationEffect
{
    private protected SyncEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Sync)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Fetch the remote of the current branch, or every remote; prunes.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="AllRemotes">Fetch every remote.</param>
public sealed record FetchEffect(TabId TabId, RepositoryLocation Location, bool AllRemotes)
    : SyncEffect(TabId, Location);

/// <summary>Pull the current branch, fast-forward only.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
public sealed record PullEffect(TabId TabId, RepositoryLocation Location) : SyncEffect(TabId, Location);

/// <summary>Push, plainly, to set the upstream, or forced with a lease.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Request">What to push.</param>
/// <param name="Done">The status text of a success.</param>
/// <param name="Branch">The branch that was checked out when the push started.</param>
public sealed record PushEffect(
    TabId TabId,
    RepositoryLocation Location,
    PushRequest Request,
    string Done,
    string Branch) : SyncEffect(TabId, Location);

/// <summary>Delete a branch on a remote, guarded by a lease on the tip the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Remote">The remote.</param>
/// <param name="Branch">The branch on it.</param>
/// <param name="CommitHash">The expected tip.</param>
public sealed record DeleteRemoteBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Remote,
    string Branch,
    string CommitHash) : SyncEffect(TabId, Location);

/// <summary>Read the configured remotes before a prompt or confirm.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Purpose">Why the remotes are read.</param>
/// <param name="Branch">The branch of a first push or a force push, null for a delete.</param>
/// <param name="DirectName">The one remote branch the cursor named for a delete, null when none.</param>
public sealed record ListSyncRemotesEffect(
    TabId TabId,
    RepositoryLocation Location,
    SyncRemotesPurpose Purpose,
    string? Branch,
    string? DirectName) : SyncEffect(TabId, Location);

/// <summary>Find out whether a local tip is in the history of the remote-tracking tip.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Branch">The branch that was pushed.</param>
/// <param name="CommitHash">The local tip.</param>
/// <param name="Remote">The remote it was pushed to.</param>
/// <param name="TrackingHash">The tip of the remote-tracking branch.</param>
public sealed record CheckBehindEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Branch,
    string CommitHash,
    string Remote,
    string TrackingHash) : SyncEffect(TabId, Location);

/// <summary>Stop the running transfer of a tab; the transfer then ends with <see cref="SyncCancelled"/>. Not an
/// operation of its own.</summary>
/// <param name="TabId">The tab.</param>
public sealed record CancelSyncEffect(TabId TabId) : AppEffect;
