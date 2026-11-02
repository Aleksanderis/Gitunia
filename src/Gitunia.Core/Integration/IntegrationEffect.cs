using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Merge;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Integration;

/// <summary>A call of a reset, a merge or the operation in progress. Each posts one result
/// <see cref="IntegrationCommand"/>; none changes state itself.</summary>
public abstract record IntegrationEffect : OperationEffect
{
    private protected IntegrationEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Integration)
    {
        Location = location;
    }

    /// <summary>Where the call runs.</summary>
    public RepositoryLocation Location { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new IntegrationFailed(TabId, text);
}

/// <summary>Read what a reset would drop (the tracked changes) and whether a stopped operation forbids it; writes
/// nothing.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the call runs.</param>
/// <param name="Target">The reset.</param>
public sealed record PrepareResetEffect(TabId TabId, RepositoryLocation Location, ResetTarget Target)
    : IntegrationEffect(TabId, Location);

/// <summary>Move the current branch with a soft, mixed or hard reset.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the call runs.</param>
/// <param name="Target">The reset.</param>
public sealed record ResetBranchEffect(TabId TabId, RepositoryLocation Location, ResetTarget Target)
    : IntegrationEffect(TabId, Location);

/// <summary>Merge a branch, tag or commit into the current branch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the call runs.</param>
/// <param name="Branch">The branch that is merged into.</param>
/// <param name="Target">What is merged.</param>
/// <param name="Mode">Fast-forward when possible, always a merge commit, or squash.</param>
public sealed record MergeBranchEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Branch,
    MergeTarget Target,
    MergeMode Mode) : IntegrationEffect(TabId, Location);

/// <summary>Continue the operation in progress.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the call runs.</param>
/// <param name="Info">The operation, as the user saw it.</param>
/// <param name="Branch">The branch it runs on, null on a detached HEAD.</param>
public sealed record ContinueOperationEffect(
    TabId TabId,
    RepositoryLocation Location,
    OperationInfo Info,
    string? Branch) : IntegrationEffect(TabId, Location);

/// <summary>Abort the operation in progress.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the call runs.</param>
/// <param name="Info">The operation, as the user saw it.</param>
/// <param name="Branch">The branch it runs on, null on a detached HEAD.</param>
public sealed record AbortOperationEffect(
    TabId TabId,
    RepositoryLocation Location,
    OperationInfo Info,
    string? Branch) : IntegrationEffect(TabId, Location);

/// <summary>Skip the commit the operation in progress stopped on.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the call runs.</param>
/// <param name="Info">The operation, as the user saw it.</param>
public sealed record SkipOperationEffect(TabId TabId, RepositoryLocation Location, OperationInfo Info)
    : IntegrationEffect(TabId, Location);
