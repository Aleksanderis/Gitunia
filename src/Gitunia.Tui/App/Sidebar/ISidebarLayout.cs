using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.App.Sidebar;

/// <summary>What the sidebar keys need of the screen layout, which only the shell knows.</summary>
public interface ISidebarLayout
{
    /// <summary>Visible sidebar rows, for the half-page motions.</summary>
    int SidebarPageHeight { get; }

    /// <summary>The panes that are shown now, in focus order (a zoomed or hidden pane is not listed).</summary>
    ImmutableArray<PaneKind> OpenPanes();
}
