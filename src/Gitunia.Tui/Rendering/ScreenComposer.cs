using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering;

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
    /// <param name="detail">The commit detail pane below the graph, or null to leave it out (it is also left out on short screens).</param>
    public static IReadOnlyList<StyledLine> Compose(
        ScreenModel model,
        CommitGraph graph,
        int selectedItem,
        int topItem,
        int width,
        int height,
        GraphGlyphSet glyphs,
        DetailPaneModel? detail = null)
    {
        var lines = new List<StyledLine>(height);
        if (height > ScreenLayout.HeaderRow)
        {
            lines.Add(ChromeBuilder.Header(model, width, glyphs));
        }

        if (height > ScreenLayout.TabRow)
        {
            lines.Add(ChromeBuilder.Tabs(model, width, glyphs));
        }

        if (model.Picker is not null)
        {
            var pickerHeight = ScreenLayout.GraphHeight(height);
            lines.AddRange(PickerComposer.Compose(model.Picker, model.HomeDirectory, width, pickerHeight, glyphs));
            if (height > ScreenLayout.GraphTop)
            {
                lines.Add(ChromeBuilder.Status(model, width, glyphs));
            }

            return lines;
        }

        var options = new GraphRowOptions(width, GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, width), glyphs);
        var showDetail = detail is not null && DetailLayout.PaneHeight(height) > 0;
        var graphHeight = showDetail ? DetailLayout.GraphHeight(height) : ScreenLayout.GraphHeight(height);
        for (var offset = 0; offset < graphHeight; offset++)
        {
            var item = topItem + offset;
            var commitIndex = item - ScreenLayout.ReservedLeadingRows;
            lines.Add(commitIndex >= 0 && commitIndex < graph.Count
                ? GraphRowRenderer.Render(graph[commitIndex], options, item == selectedItem)
                : GraphRowRenderer.RenderEmpty(options, item == selectedItem && graph.Count > 0));
        }

        if (showDetail)
        {
            lines.AddRange(DetailPaneRenderer.RenderPane(detail!, width, DetailLayout.ContentHeight(height), glyphs));
        }

        if (height > ScreenLayout.GraphTop)
        {
            lines.Add(ChromeBuilder.Status(model, width, glyphs));
        }

        return lines;
    }
}
