using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Discard;

/// <summary>The facts of the root the discard reducer reads.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="FocusedPane">The pane that has the focus in the tab.</param>
/// <param name="Tree">The working tree as the lists show it, null until the status is loaded.</param>
/// <param name="IsBusy">A discard, stage or unstage already runs for the tab (it ends when the status was read again).</param>
/// <param name="IsBlocked">Another operation that writes the index or the working tree runs for the tab.</param>
/// <param name="CanDeleteInvalidNames">This OS can delete an untracked name that is not valid UTF-8.</param>
public sealed record DiscardContext(
    TabId TabId,
    RepositoryLocation Location,
    PaneKind FocusedPane,
    WorkingTreeState? Tree,
    bool IsBusy,
    bool IsBlocked,
    bool CanDeleteInvalidNames);
