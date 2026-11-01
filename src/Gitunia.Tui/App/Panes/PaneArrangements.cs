using Gitunia.App;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>Which panes are shown and how big, for the active tab: the state of the diff pane and the saved pane sizes.</summary>
/// <param name="host">Holds the active tab.</param>
/// <param name="sessions">The shown tab's runtime session.</param>
public sealed class PaneArrangements(Dispatcher host, IActiveSessions sessions) : IPaneArrangement
{
    /// <summary>The terminal shows the diff beside the file list (the Changes and Files tabs on a roomy screen).</summary>
    public bool ShowsWithChanges() =>
        host.Workspace.ActiveTab is { } tab
        && DiffPaneRules.ShowsWithChanges(host.State.Shell.Screen, tab.View.BottomTab);

    /// <summary>The Files tab is showing and the screen has room for its content beside the tree.</summary>
    public bool IsFilesBeside() =>
        host.Workspace.ActiveTab?.View.BottomTab == BottomTab.Files && ShowsWithChanges();

    /// <summary>The pane is on screen: opened on demand, or shown by the Changes tab.</summary>
    public bool IsShown() => ActiveState().IsOpen || ShowsWithChanges();

    /// <summary>The state of the active tab's diff pane.</summary>
    public DiffPaneState ActiveState() => host.Workspace.ActiveTab?.View.DiffPane ?? DiffPaneState.Empty;

    /// <summary>The panes' arrangement for the active tab; a zoom that does not match the focused pane is not part of it.</summary>
    public PaneArrangement Arrangement()
    {
        if (sessions.ActiveSession()?.Diff is null)
        {
            return PaneArrangement.Normal with { Sizes = SizesOfActiveTab() };
        }

        var diff = ActiveState();
        var focused = host.Workspace.ActiveTab?.View.FocusedPane;
        var shows = ShowsWithChanges();
        var together = DiffPaneRules.KeepsZoomTogether(diff.Zoomed, focused, shows);
        var zoomed = DiffPaneRules.EffectiveZoom(diff, focused, shows);

        return new PaneArrangement(DiffPaneRules.IsShown(diff, shows), zoomed, SizesOfActiveTab(), together);
    }

    /// <summary>The pane sizes saved in the active tab's view state.</summary>
    public PaneSizes SizesOfActiveTab() => host.Workspace.ActiveTab?.View is { } view
        ? new PaneSizes(view.SidebarWidth, view.BottomHeight, view.DetailWidth)
        : PaneSizes.Automatic;
}
