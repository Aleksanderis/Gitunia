using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;

namespace Gitunia.Core.Stash;

/// <summary>The facts of the root the stash reducer reads.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Stashes">The stash stack of the tab, newest first.</param>
/// <param name="FocusedPane">The pane that has the focus in the tab.</param>
/// <param name="IsBusy">A stash operation already runs for the tab (read from the tab's running operations).</param>
public sealed record StashContext(
    TabId TabId,
    RepositoryLocation Location,
    ImmutableArray<StashEntry> Stashes,
    PaneKind FocusedPane,
    bool IsBusy);
