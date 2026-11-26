using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Ignore;

/// <summary>The facts of the root the ignore reducer reads.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="FocusedPane">The pane that has the focus in the tab.</param>
/// <param name="Tree">The working tree as the lists show it, null until the status is loaded.</param>
/// <param name="IsBusy">An ignore already runs for the tab (it ends when the status was read again).</param>
public sealed record IgnoreContext(
    TabId TabId,
    RepositoryLocation Location,
    PaneKind FocusedPane,
    WorkingTreeState? Tree,
    bool IsBusy);
