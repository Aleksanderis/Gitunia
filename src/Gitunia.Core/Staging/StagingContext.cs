using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Staging;

/// <summary>The facts of the root the staging reducer reads.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="FocusedPane">The pane that has the focus in the tab.</param>
/// <param name="Tree">The working tree as the lists show it, null until the status is loaded.</param>
/// <param name="HasOperation">A merge, rebase or similar is in progress in the tab.</param>
/// <param name="IsBusy">A stage or unstage already runs for the tab (it ends when the status was read again).</param>
/// <param name="IsBlocked">Another operation that writes the index or the working tree runs for the tab.</param>
public sealed record StagingContext(
    TabId TabId,
    RepositoryLocation Location,
    PaneKind FocusedPane,
    WorkingTreeState? Tree,
    bool HasOperation,
    bool IsBusy,
    bool IsBlocked);
