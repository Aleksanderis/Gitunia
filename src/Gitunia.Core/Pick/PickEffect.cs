using System.Collections.Immutable;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Pick;

/// <summary>An operation of a cherry-pick or revert. Each posts one result <see cref="PickCommand"/>.</summary>
public abstract record PickEffect : OperationEffect
{
    private protected PickEffect(TabId tabId, RepositoryLocation location, PickKind pickKind)
        : base(tabId, RunningOperationKind.Pick)
    {
        Location = location;
        PickKind = pickKind;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }

    /// <summary>Cherry-pick or revert.</summary>
    public PickKind PickKind { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new PickFailed(TabId, text);
}

/// <summary>Read the commits (subjects, parents of a merge commit) before anything runs.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="PickKind">Cherry-pick or revert.</param>
/// <param name="Hashes">The commits in apply order.</param>
public sealed record ReadPickCommitsEffect(
    TabId TabId,
    RepositoryLocation Location,
    PickKind PickKind,
    ImmutableArray<string> Hashes) : PickEffect(TabId, Location, PickKind);

/// <summary>Cherry-pick or revert the commits onto the current branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="PickKind">Cherry-pick or revert.</param>
/// <param name="Commits">The commits in apply order.</param>
/// <param name="Mainline">The parent number to keep for a merge commit, null when there is none.</param>
/// <param name="Target">How the branch is named in messages: its name, or the detached HEAD.</param>
public sealed record RunPickEffect(
    TabId TabId,
    RepositoryLocation Location,
    PickKind PickKind,
    ImmutableArray<PickCommit> Commits,
    int? Mainline,
    string Target) : PickEffect(TabId, Location, PickKind);
