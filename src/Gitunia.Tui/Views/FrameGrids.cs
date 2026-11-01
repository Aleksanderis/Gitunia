using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.Views;

/// <summary>
/// The frame lines of the screen (the boxes around the panes), rebuilt only when what they depend on changed: the size,
/// the layout, the focus, the titles.
/// </summary>
/// <param name="panes">The panes whose focus and titles the grid shows.</param>
/// <param name="geometry">Where the panes are.</param>
/// <param name="mode">Which panes are on screen.</param>
/// <param name="content">The model the titles come from.</param>
public sealed class FrameGrids(PaneViews panes, PaneGeometry geometry, ScreenMode mode, IScreenContent content)
{
    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    private GridKey? _gridKey;
    private FrameGrid? _grid;
    private WorkingGridKey? _workingGridKey;
    private FrameGrid? _workingGrid;

    /// <summary>The grid of the screen that is shown now.</summary>
    public FrameGrid Current() => mode.ShowsWorkingPanes ? CurrentWorkingGrid() : CurrentHistoryGrid();

    /// <summary>Forgets the built grids (the screen switched between the history panes and the Changes view).</summary>
    public void Reset()
    {
        _workingGrid = null;
        _grid = null;
    }

    private FrameGrid CurrentWorkingGrid()
    {
        var glyphs = panes.Content.Glyphs;
        var focus = geometry.WorkingFocus;
        var rects = geometry.WorkingRects();
        var unstagedTitle = WorkingTreePaneRenderer.ListTitle(panes.UnstagedSource.Tree, WorkingTreeSection.Unstaged);
        var stagedTitle = WorkingTreePaneRenderer.ListTitle(panes.StagedSource.Tree, WorkingTreeSection.Staged);
        var messageTitle = panes.Content.MessageBox.Draft?.Title ?? WorkingTreePaneRenderer.MessageTitle;
        var key = new WorkingGridKey(
            geometry.ScreenWidth,
            geometry.ScreenHeight,
            rects,
            focus,
            unstagedTitle,
            stagedTitle,
            messageTitle,
            glyphs.IsAscii);
        if (_workingGrid is null || _workingGridKey != key)
        {
            _workingGrid = FrameGrid.Build(
                geometry.ScreenWidth,
                geometry.ScreenHeight,
                [
                    new FrameBox(rects.Unstaged, focus == PaneKind.WorkingUnstaged, unstagedTitle),
                    new FrameBox(rects.Staged, focus == PaneKind.WorkingStaged, stagedTitle),
                    new FrameBox(rects.Diff, WorkingPanes.IsDiff(focus), string.Empty),
                    new FrameBox(rects.Message, focus == PaneKind.WorkingMessage, messageTitle),
                ],
                glyphs);
            _workingGridKey = key;
        }

        return _workingGrid;
    }

    private FrameGrid CurrentHistoryGrid()
    {
        var glyphs = panes.Content.Glyphs;
        var model = content.Model();
        var isPicker = mode.IsPicker;
        var rects = isPicker ? PickerRects() : geometry.Rects();
        var focused = FocusedHistoryPane(isPicker);
        var title = model.RepositoryName.Length > 0 ? model.RepositoryName : "Repository";
        var key = new GridKey(
            geometry.ScreenWidth,
            geometry.ScreenHeight,
            rects,
            focused,
            title,
            isPicker,
            glyphs.IsAscii,
            model.RepeatPane);
        if (_grid is null || _gridKey != key)
        {
            var sidebarTitle = isPicker ? string.Empty : RepeatMode.Mark(title, key.RepeatPane == PaneKind.Sidebar);
            var graphTitle = isPicker ? string.Empty : RepeatMode.Mark("History", key.RepeatPane == PaneKind.Graph);
            _grid = FrameGrid.Build(
                geometry.ScreenWidth,
                geometry.ScreenHeight,
                rects,
                focused,
                glyphs,
                sidebarTitle,
                graphTitle);
            _gridKey = key;
        }

        return _grid;
    }

    private PaneRects PickerRects()
    {
        var graph = new PaneRect(
            1,
            ScreenLayout.GraphTop,
            ScreenLayout.InnerWidth(geometry.ScreenWidth),
            ScreenLayout.GraphHeight(geometry.ScreenHeight));

        return new PaneRects(Empty, Empty, Empty, graph, Empty, Empty, Empty, Empty, Empty);
    }

    private PaneKind FocusedHistoryPane(bool isPicker)
    {
        if (isPicker)
        {
            return PaneKind.Graph;
        }

        if (panes.Sidebar.HasFocus)
        {
            return PaneKind.Sidebar;
        }

        if (panes.Detail.HasFocus)
        {
            return PaneKind.Detail;
        }

        return panes.Diff.HasFocus ? PaneKind.Diff : PaneKind.Graph;
    }

    private readonly record struct WorkingGridKey(
        int Width,
        int Height,
        WorkingTreeRects Rects,
        PaneKind Focus,
        string UnstagedTitle,
        string StagedTitle,
        string MessageTitle,
        bool IsAscii);
}
