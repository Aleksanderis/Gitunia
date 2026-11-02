using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Input;

/// <summary>The keymap view of the focused pane (<c>Graph</c>, <c>Detail</c>, <c>Sidebar</c>, <c>Diff</c>).</summary>
/// <param name="host">Holds the active tab and its focused pane.</param>
/// <param name="detailList">The detail list, whose toolkit focus tells the graph from the detail pane.</param>
public sealed class FocusedKeymapView(Dispatcher host, ListView detailList) : IFocusedView
{
    /// <inheritdoc />
    public string Name => host.Workspace.ActiveTab?.View.FocusedPane switch
    {
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        PaneKind.Diff => DefaultKeymap.DiffView,
        PaneKind.WorkingUnstaged => DefaultKeymap.WorkingUnstagedView,
        PaneKind.WorkingStaged => DefaultKeymap.WorkingStagedView,
        PaneKind.WorkingUnstagedDiff => DefaultKeymap.WorkingUnstagedDiffView,
        PaneKind.WorkingStagedDiff => DefaultKeymap.WorkingStagedDiffView,
        PaneKind.WorkingMessage => DefaultKeymap.WorkingMessageView,
        _ => detailList.HasFocus ? DetailViewName() : DefaultKeymap.GraphView,
    };

    private string DetailViewName() => host.Workspace.ActiveTab?.View.BottomTab switch
    {
        BottomTab.Changes => DefaultKeymap.DetailChangesView,
        BottomTab.Files => DefaultKeymap.DetailFilesView,
        _ => DefaultKeymap.DetailView,
    };
}
