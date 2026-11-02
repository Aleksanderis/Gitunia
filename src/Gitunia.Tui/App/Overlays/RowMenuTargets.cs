using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Tui.App.Selection;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The actions menu of the row under the cursor of the graph or the sidebar, hanging at that row.</summary>
/// <param name="panes">The lists, which know where their cursor is on screen.</param>
/// <param name="anchors">Where the view dropdowns hang.</param>
/// <param name="graph">The commit selected in the graph.</param>
/// <param name="rows">The sidebar row under the cursor.</param>
public sealed class RowMenuTargets(PaneViews panes, MenuAnchors anchors, GraphSelection graph, RowSelection rows)
    : IRowMenuTargets
{
    /// <inheritdoc />
    public RowMenuTarget? TargetOf(string view)
    {
        if (view == DefaultKeymap.DiffView)
        {
            return DiffTarget();
        }

        if (view == DefaultKeymap.WorkingUnstagedDiffView || view == DefaultKeymap.WorkingStagedDiffView)
        {
            return new RowMenuTarget(
                RowActions.ViewOptions,
                anchors.ViewMenuAnchor(isWorking: true),
                RowActions.ViewOptionsTitle);
        }

        if (view == DefaultKeymap.DetailChangesView
            || view == DefaultKeymap.WorkingUnstagedView
            || view == DefaultKeymap.WorkingStagedView)
        {
            return new RowMenuTarget(
                RowActions.PathStyleOptions,
                anchors.ListViewMenuAnchor(view),
                RowActions.ListViewOptionsTitle);
        }

        if (view == DefaultKeymap.DetailFilesView)
        {
            return new RowMenuTarget(
                RowActions.FilesTabViewOptions,
                anchors.ListViewMenuAnchor(view),
                RowActions.ListViewOptionsTitle);
        }

        if (view == DefaultKeymap.SidebarView)
        {
            return SidebarTarget();
        }

        if (view == DefaultKeymap.GraphView && graph.GraphCommitHash() is not null)
        {
            return new RowMenuTarget(
                RowActions.OfGraphRowFor(
                    DetailLayout.PaneHeight(panes.Chrome.ScreenSize.Height) > 0,
                    graph.IsSelectedCommitRoot()),
                AnchorOf(panes.Graph));
        }

        return null;
    }

    private static MenuAnchor AnchorOf(PaneListView list)
    {
        var lastRow = Math.Max(0, list.Viewport.Height - 1);
        var visibleRow = Math.Clamp((list.SelectedItem ?? 0) - list.Viewport.Y, 0, lastRow);

        return new MenuAnchor(list.Frame.X, list.Frame.Y + visibleRow);
    }

    private RowMenuTarget? DiffTarget()
    {
        if (panes.Diff.Source is DiffListSource { Document.IsFileContent: true })
        {
            return null;
        }

        return new RowMenuTarget(
            RowActions.ViewOptions,
            anchors.ViewMenuAnchor(isWorking: false),
            RowActions.ViewOptionsTitle);
    }

    private RowMenuTarget? SidebarTarget()
    {
        if (rows.SelectedSidebarRow() is { IsFolder: false } row)
        {
            return new RowMenuTarget(RowActions.OfSidebarRow(row), AnchorOf(panes.Sidebar));
        }

        return null;
    }
}
