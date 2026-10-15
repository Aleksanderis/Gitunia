using Gitunia.Core.Graph;
using Gitunia.Core.Search.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>Lays out a whole frame as styled lines, without a terminal: what snapshot tests and <c>--print-frame</c> show.</summary>
public static class ScreenComposer
{
    /// <param name="model">The chrome content.</param>
    /// <param name="graph">The loaded commits.</param>
    /// <param name="selectedItem">Selected list item (the reserved leading rows count as items).</param>
    /// <param name="topItem">First visible list item.</param>
    /// <param name="width">Terminal columns.</param>
    /// <param name="height">Terminal rows.</param>
    /// <param name="glyphs">The characters to draw the graph with.</param>
    /// <param name="sidebar">The sidebar to show left of the graph (when the terminal is wide enough), or null for none.</param>
    /// <param name="highlight">The active graph search whose matches are highlighted, null when none.</param>
    /// <param name="detail">The commit detail pane below the graph, or null to leave it out (it is also left out on short screens).</param>
    /// <param name="diff">The diff pane, or null when none is open (then <paramref name="arrangement"/> must not open or zoom the diff).</param>
    /// <param name="arrangement">Whether the diff is open and which pane is zoomed, null for the normal layout.</param>
    public static IReadOnlyList<StyledLine> Compose(
        ScreenModel model,
        CommitGraph graph,
        int selectedItem,
        int topItem,
        int width,
        int height,
        GraphGlyphSet glyphs,
        DetailPaneModel? detail = null,
        SidebarModel? sidebar = null,
        SearchQuery? highlight = null,
        DiffPaneModel? diff = null,
        PaneArrangement? arrangement = null)
    {
        var lines = new List<StyledLine>(height);
        if (height > ScreenLayout.TabRow)
        {
            lines.Add(ChromeBuilder.Tabs(model, width, glyphs));
        }

        var layout = arrangement ?? PaneArrangement.Normal;
        var isPicker = model.Picker is not null;
        var rects = isPicker
            ? PickerRects(width, height)
            : PaneLayout.Compute(width, height, layout.IsDiffOpen && diff is not null, layout.Zoomed, layout.Sizes);
        var focused = FocusedPane(sidebar, detail, diff);
        var sidebarTitle = model.RepositoryName.Length > 0 ? model.RepositoryName : "Repository";
        var grid = FrameGrid.Build(width, height, rects, isPicker ? PaneKind.Graph : focused, glyphs, isPicker ? string.Empty : sidebarTitle, isPicker ? string.Empty : "History");
        var overlays = new List<PaneLines>();
        if (isPicker)
        {
            overlays.Add(new PaneLines(rects.Graph.X, rects.Graph.Y, PickerComposer.Compose(model.Picker!, model.HomeDirectory, rects.Graph.Width, rects.Graph.Height, glyphs), SegmentSelection.Focused));
        }
        else
        {
            AddPanes(overlays, graph, selectedItem, topItem, glyphs, detail, sidebar, highlight, diff, rects);
        }

        for (var row = ScreenLayout.FrameTop; row < height - 1; row++)
        {
            lines.Add(BuildRow(grid, row, width, overlays));
        }

        if (height > ScreenLayout.TabRow + 1)
        {
            lines.Add(ChromeBuilder.Status(model, width, glyphs));
        }

        return lines;
    }

    private static void AddPanes(
        List<PaneLines> overlays,
        CommitGraph graph,
        int selectedItem,
        int topItem,
        GraphGlyphSet glyphs,
        DetailPaneModel? detail,
        SidebarModel? sidebar,
        SearchQuery? highlight,
        DiffPaneModel? diff,
        PaneRects rects)
    {
        var showDetail = detail is not null && !rects.Detail.IsEmpty;
        var showDiff = diff is not null && !rects.Diff.IsEmpty;
        if (!rects.Graph.IsEmpty)
        {
            var graphHeight = rects.Graph.Height;
            var textWidth = PanePadding.TextWidth(rects.Graph.Width);
            var options = new GraphRowOptions(textWidth, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, textWidth), glyphs, highlight);
            var graphLines = new List<StyledLine>(graphHeight);
            for (var offset = 0; offset < graphHeight; offset++)
            {
                var item = topItem + offset;
                var commitIndex = item - ScreenLayout.ReservedLeadingRows;
                graphLines.Add(PanePadding.Wrap(commitIndex >= 0 && commitIndex < graph.Count
                    ? GraphRowRenderer.Render(graph[commitIndex], options, item == selectedItem)
                    : GraphRowRenderer.RenderEmpty(options, item == selectedItem && graph.Count > 0)));
            }

            overlays.Add(new PaneLines(rects.Graph.X, rects.Graph.Y, graphLines, SegmentSelection.Focused));
        }

        if (sidebar is not null && !rects.Sidebar.IsEmpty)
        {
            var sidebarLines = SidebarPane.Compose(sidebar, PanePadding.TextWidth(rects.Sidebar.Width), rects.Sidebar.Height + rects.SidebarFilter.Height, glyphs);
            overlays.Add(new PaneLines(rects.Sidebar.X, rects.Sidebar.Y, [.. sidebarLines.Select(PanePadding.Wrap)], sidebar.IsFocused ? SegmentSelection.Focused : SegmentSelection.Dim));
        }

        if (showDetail)
        {
            overlays.Add(new PaneLines(rects.DetailTitle.X, rects.DetailTitle.Y, DetailPaneRenderer.RenderPane(detail!, rects.DetailTitle.Width, rects.Detail.Height, glyphs), SegmentSelection.Focused));
        }

        if (showDiff)
        {
            overlays.Add(new PaneLines(rects.DiffTitle.X, rects.DiffTitle.Y, DiffPaneRenderer.RenderPane(diff!, rects.DiffTitle.Width, rects.Diff.Height, glyphs), SegmentSelection.Focused));
        }
    }

    // The picker takes the place of the panes: one box between the edges.
    private static PaneRects PickerRects(int width, int height)
    {
        var none = new PaneRect(0, 0, 0, 0);
        return new PaneRects(none, none, none, new PaneRect(1, ScreenLayout.GraphTop, ScreenLayout.InnerWidth(width), ScreenLayout.GraphHeight(height)), none, none, none, none, none);
    }

    private static PaneKind FocusedPane(SidebarModel? sidebar, DetailPaneModel? detail, DiffPaneModel? diff) =>
        sidebar is { IsFocused: true } ? PaneKind.Sidebar : detail is { IsFocused: true } ? PaneKind.Detail : diff is { IsFocused: true } ? PaneKind.Diff : PaneKind.Graph;

    // One screen row: the frame's cells with the panes' lines laid over them where they sit.
    private static StyledLine BuildRow(FrameGrid grid, int row, int width, List<PaneLines> overlays)
    {
        var segments = new List<StyledSegment>();
        var column = 0;
        foreach (var overlay in overlays.Where(overlay => row >= overlay.Top && row < overlay.Top + overlay.Lines.Count).OrderBy(overlay => overlay.Left))
        {
            if (overlay.Left > column)
            {
                segments.AddRange(grid.Row(row, column, overlay.Left - column).Segments);
            }

            var line = grid.ResolveRules(overlay.Lines[row - overlay.Top], row, overlay.Left);
            var selection = line.IsSelected ? overlay.Selection : SegmentSelection.None;
            segments.AddRange(line.Segments.Select(segment => segment with { Selection = selection }));
            column = overlay.Left + line.Segments.Sum(segment => TextWidth.Of(segment.Text));
        }

        if (column < width)
        {
            segments.AddRange(grid.Row(row, column, width - column).Segments);
        }

        return new StyledLine(segments);
    }
}
