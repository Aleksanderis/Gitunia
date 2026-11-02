using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Branches;

/// <summary>An operation on branches. Each posts one result <see cref="BranchCommand"/>; none changes state.</summary>
public abstract record BranchEffect : OperationEffect
{
    private protected BranchEffect(TabId tabId, RepositoryLocation location, string? follow)
        : base(tabId, RunningOperationKind.Branches)
    {
        Location = location;
        Follow = follow;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }

    /// <summary>The commit the graph cursor follows to after a success, null to leave it.</summary>
    public string? Follow { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new BranchOperationFailed(TabId, text);
}

/// <summary>Create a branch, optionally checking it out.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The new branch.</param>
/// <param name="StartPoint">The ref or commit it starts at.</param>
/// <param name="Label">How the start point is named for the user.</param>
/// <param name="Checkout">True to check it out.</param>
/// <param name="Follow">The commit the graph cursor follows to.</param>
public sealed record CreateBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Name,
    string StartPoint,
    string Label,
    bool Checkout,
    string? Follow) : BranchEffect(TabId, Location, Follow);

/// <summary>Check out a local branch, a remote branch (as a new tracking branch) or a commit (detached).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="TargetKind">What is checked out.</param>
/// <param name="Target">The branch name (<c>origin/topic</c> for a remote one) or the commit hash.</param>
/// <param name="Follow">The commit the graph cursor follows to.</param>
public sealed record SwitchBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    SwitchTargetKind TargetKind,
    string Target,
    string? Follow) : BranchEffect(TabId, Location, Follow);

/// <summary>Rename a local branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="From">The current name.</param>
/// <param name="Name">The new name.</param>
/// <param name="Follow">The commit the graph cursor follows to.</param>
public sealed record RenameBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    string From,
    string Name,
    string? Follow) : BranchEffect(TabId, Location, Follow);

/// <summary>Delete a local branch; without <paramref name="Force"/> one with unreachable commits stays.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The branch.</param>
/// <param name="CommitHash">Where it points, for the restore hint.</param>
/// <param name="UpstreamFullName">Its upstream, which counts as reaching its commits; null when it has none.</param>
/// <param name="Force">True when the user answered the unmerged question.</param>
/// <param name="Follow">The commit the graph cursor follows to.</param>
public sealed record DeleteBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Name,
    string CommitHash,
    string? UpstreamFullName,
    bool Force,
    string? Follow) : BranchEffect(TabId, Location, Follow);

/// <summary>Make a local branch track a remote branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The local branch.</param>
/// <param name="Upstream">The remote branch, such as <c>origin/topic</c>.</param>
public sealed record SetUpstreamEffect(TabId TabId, RepositoryLocation Location, string Name, string Upstream)
    : BranchEffect(TabId, Location, null);

/// <summary>Make a local branch track nothing.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The local branch.</param>
/// <param name="Upstream">The upstream it had, as shown to the user (for the restore hint).</param>
public sealed record UnsetUpstreamEffect(TabId TabId, RepositoryLocation Location, string Name, string Upstream)
    : BranchEffect(TabId, Location, null);

/// <summary>Move a local branch that is not checked out to its upstream as last fetched.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Name">The branch.</param>
/// <param name="UpstreamFullName">Its upstream.</param>
/// <param name="OldHash">Where the branch is now, for the undo hint.</param>
/// <param name="NewHash">Where the upstream is.</param>
public sealed record FastForwardBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Name,
    string UpstreamFullName,
    string OldHash,
    string NewHash) : BranchEffect(TabId, Location, null);
