using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;

namespace Gitunia.Core.Stash;

/// <summary>An operation on the stash stack. Each posts one result <see cref="StashCommand"/>; none changes state
/// itself.</summary>
public abstract record StashEffect : OperationEffect
{
    private protected StashEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Stash)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Stash the changes (<c>git stash push</c>).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Message">The message, empty for git's default.</param>
/// <param name="IncludeUntracked">Also stash the untracked files.</param>
public sealed record PushStashEffect(TabId TabId, RepositoryLocation Location, string Message, bool IncludeUntracked)
    : StashEffect(TabId, Location);

/// <summary>Apply a stash, and drop it after a clean apply when popping.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Entry">The stash, named by its commit.</param>
/// <param name="Pop">Drop the stash after a clean apply.</param>
public sealed record ApplyStashEffect(TabId TabId, RepositoryLocation Location, StashEntry Entry, bool Pop)
    : StashEffect(TabId, Location);

/// <summary>Drop a stash.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Entry">The stash, named by its commit.</param>
public sealed record DropStashEffect(TabId TabId, RepositoryLocation Location, StashEntry Entry)
    : StashEffect(TabId, Location);

/// <summary>Read what restoring one file would do; writes nothing.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Target">The file.</param>
public sealed record CheckStashRestoreEffect(TabId TabId, RepositoryLocation Location, StashFileTarget Target)
    : StashEffect(TabId, Location);

/// <summary>Copy one file's stashed version into the working tree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Target">The file.</param>
public sealed record RestoreStashFileEffect(TabId TabId, RepositoryLocation Location, StashFileTarget Target)
    : StashEffect(TabId, Location);
