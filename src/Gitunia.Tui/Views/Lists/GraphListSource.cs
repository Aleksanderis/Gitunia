using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Graph;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Search.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>
/// Append-only list source over a <see cref="CommitGraph"/>: rows are drawn on demand for the visible range only,
/// so the cost of a frame does not depend on how many commits are loaded. Appending raises one change event per batch.
/// </summary>
public sealed class GraphListSource(CommitGraph graph, GraphGlyphSet glyphs) : IListDataSource, IRangeSource
{
    private readonly Dictionary<int, JumpLabel> _jumpLabels = [];
    private GraphRowOptions? _cachedOptions;
    private JumpKey? _jumpKey;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Items of the list: the reserved leading rows plus the loaded commits.</summary>
    public int Count => ScreenLayout.ReservedLeadingRows + graph.Count;

    public int MaxItemLength => 0;

    /// <summary>The active graph search whose matches are highlighted, null when none.</summary>
    public SearchQuery? Highlight { get; set; }

    /// <summary>The text of an open quick jump, whose targets in view get labels; null when none is open.</summary>
    public string? JumpText { get; set; }

    public bool SuspendCollectionChangedEvent { get; set; }

    public RowRange? Range { get; set; }

    // The graph only grows at the end, so a range over it never changes meaning.
    public event Action? RangeInvalidated
    {
        add { }
        remove { }
    }

    /// <summary>The commit shown by a list item, or -1 for a reserved leading row or an item past the loaded end.</summary>
    public static int CommitIndexOf(int item) => item - ScreenLayout.ReservedLeadingRows;

    /// <summary>Tells the list that commits were appended to the graph.</summary>
    public void NotifyAppended()
    {
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var columns = ScreenPadding.Columns(listView);
        var options = OptionsFor(PanePadding.TextWidth(width, columns));
        var commitIndex = CommitIndexOf(item);
        selected |= Range?.Contains(item) == true;
        var line = commitIndex >= 0 && commitIndex < graph.Count
            ? GraphRowRenderer.Render(graph[commitIndex], options, selected, JumpLabelOf(listView, commitIndex))
            : GraphRowRenderer.RenderEmpty(options, selected);
        LinePainter.Paint(listView, PanePadding.Wrap(line, columns), col, row, listView.HasFocus);
    }

    public void Dispose()
    {
    }

    // The labels are worked out once per frame (the first visible row), not once per row.
    private JumpLabel? JumpLabelOf(ListView listView, int commitIndex)
    {
        if (string.IsNullOrEmpty(JumpText))
        {
            return null;
        }

        var first = CommitIndexOf(listView.Viewport.Y);
        var cursor = CommitIndexOf(listView.SelectedItem ?? 0);
        var key = new JumpKey(JumpText, first, listView.Viewport.Height, cursor, graph.Count);
        if (_jumpKey != key)
        {
            _jumpKey = key;
            _jumpLabels.Clear();
            foreach (var label in QuickJumpLabeler.Compute(JumpText, graph, first, listView.Viewport.Height, cursor))
            {
                _jumpLabels[label.Row] = label;
            }
        }

        return _jumpLabels.TryGetValue(commitIndex, out var found) ? found : null;
    }

    private GraphRowOptions OptionsFor(int width)
    {
        var laneColumns = GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, width);
        if (_cachedOptions is null || _cachedOptions.Width != width || _cachedOptions.LaneColumns != laneColumns || _cachedOptions.Highlight != Highlight)
        {
            _cachedOptions = new GraphRowOptions(width, laneColumns, glyphs, Highlight);
        }

        return _cachedOptions;
    }
}
