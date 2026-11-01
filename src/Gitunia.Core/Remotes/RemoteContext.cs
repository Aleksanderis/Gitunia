using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Remotes;

/// <summary>The facts of the root the remote reducer reads: the tab, its refs and whether it is the active tab.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Refs">The refs the tab shows.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsBusy">A remote operation already runs for the tab (read from the tab's running operations).</param>
/// <param name="IsSyncing">A fetch, pull or push runs for the tab: remotes stay as they are meanwhile.</param>
public sealed record RemoteContext(
    TabId TabId,
    RepositoryLocation Location,
    ImmutableArray<GitRef> Refs,
    bool IsActive,
    bool IsBusy,
    bool IsSyncing = false);
