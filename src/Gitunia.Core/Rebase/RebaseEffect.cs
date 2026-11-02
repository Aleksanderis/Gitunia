using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Rebase;

/// <summary>An operation of the rebase onto a commit or of a quick rebase. Each posts one result command.</summary>
public abstract record RebaseEffect : OperationEffect
{
    private protected RebaseEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Rebase)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new RebaseFailed(TabId, text);
}

/// <summary>Count the merge commits a rebase onto the target would flatten, for the confirm dialog.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Target">What the rebase acts on.</param>
public sealed record CountRebaseMergesEffect(TabId TabId, RepositoryLocation Location, RebaseTarget Target)
    : RebaseEffect(TabId, Location);

/// <summary>Rebase the current branch onto the target.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Target">What the rebase acts on.</param>
public sealed record RunRebaseEffect(TabId TabId, RepositoryLocation Location, RebaseTarget Target)
    : RebaseEffect(TabId, Location);

/// <summary>Read the commit and its range for a fixup, reword or drop.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="QuickKind">Fixup, reword or drop.</param>
/// <param name="CommitHash">The commit selected in the graph.</param>
public sealed record PlanQuickRebaseEffect(
    TabId TabId,
    RepositoryLocation Location,
    QuickRebaseKind QuickKind,
    string CommitHash) : RebaseEffect(TabId, Location);

/// <summary>Run the planned fixup, reword or drop.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Target">The plan, the branch and the typed subject.</param>
public sealed record RunQuickRebaseEffect(TabId TabId, RepositoryLocation Location, QuickRebaseTarget Target)
    : RebaseEffect(TabId, Location);
