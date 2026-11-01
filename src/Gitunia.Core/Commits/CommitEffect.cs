using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Commits;

/// <summary>Create the commit. Posts <see cref="CommitSucceeded"/> or <see cref="CommitFailed"/>.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the commit is made.</param>
/// <param name="Request">The message and options, as the user saw them.</param>
/// <param name="ForcePushUpstream">The upstream an amend of HEAD would need a force push to, null otherwise.</param>
public sealed record RunCommitEffect(
    TabId TabId,
    RepositoryLocation Location,
    CommitRequest Request,
    string? ForcePushUpstream) : OperationEffect(TabId, RunningOperationKind.Commit);

/// <summary>Stop the commit of the tab that runs; its result is the cancel result. Nothing happens when none
/// runs.</summary>
/// <param name="TabId">The tab.</param>
public sealed record CancelCommitEffect(TabId TabId) : AppEffect;

/// <summary>A read for the message box. It is not an operation of the tab: it ends with its own result
/// command.</summary>
public abstract record CommitReadEffect : AppEffect
{
    private protected CommitReadEffect(TabId tabId, RepositoryLocation location)
    {
        TabId = tabId;
        Location = location;
    }

    /// <summary>The tab the read is for.</summary>
    public TabId TabId { get; }

    /// <summary>Where the read runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Read the full message of HEAD (an amend fills an empty box with it). Posts <see cref="HeadMessageRead"/> or
/// <see cref="HeadMessageReadFailed"/>.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where HEAD is.</param>
/// <param name="Hash">HEAD now, handed back with the message.</param>
public sealed record ReadHeadMessageEffect(TabId TabId, RepositoryLocation Location, string? Hash)
    : CommitReadEffect(TabId, Location);

/// <summary>Read the message prepared for finishing a merge. Posts <see cref="MergeMessageRead"/> when there is
/// one.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the merge is.</param>
public sealed record ReadMergeMessageEffect(TabId TabId, RepositoryLocation Location)
    : CommitReadEffect(TabId, Location);
