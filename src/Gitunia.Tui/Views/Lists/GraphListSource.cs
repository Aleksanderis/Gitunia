using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Graph;
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
    private readonly JumpLabelCache _jump = new();
    private GraphRowOptions? _cachedOptions;
    private IReadOnlySet<string>? _upstreamGoneBranches;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Items of the list: the reserved leading rows plus the loaded commits.</summary>
    public int Count => ScreenLayout.ReservedLeadingRows + graph.Count + (ShowsCutMarker ? 1 : 0);

    /// <summary>
    /// The log is cut at the soft row limit and idle: one extra item after the last commit shows the continues-below
    /// marker (#555). It holds no commit, so <see cref="CommitIndexOf"/> gives <c>graph.Count</c> for it and every
    /// consumer bounds-checks that against the graph.
    /// </summary>
    public bool ShowsCutMarker { get; set; }

    public int MaxItemLength => 0;

    /// <summary>The active graph search whose matches are highlighted, null when none.</summary>
    public SearchQuery? Highlight { get; set; }

    /// <summary>Local branches whose upstream is gone; their graph labels get a marker. Null for none.</summary>
    public IReadOnlySet<string>? UpstreamGoneBranches
    {
        get => _upstreamGoneBranches;
        set
        {
            _upstreamGoneBranches = value;

            // The marker changes the width of the ref labels, so quick-jump labels worked out before are stale.
            _jump.Invalidate();
        }
    }

    /// <summary>The text of an open quick jump, whose targets in view get labels; null when none is open.</summary>
    public string? JumpText
    {
        get => _jump.Text;
        set => _jump.Text = value;
    }

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
        _jump.Invalidate();
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>The quick-jump targets of <paramref name="text"/> among the commits in view of <paramref name="listView"/>, nearest the cursor first.</summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="text">The typed text.</param>
    public IReadOnlyList<JumpLabel> JumpLabels(ListView listView, string text)
    {
        var first = CommitIndexOf(listView.Viewport.Y);
        var cursor = CommitIndexOf(listView.SelectedItem ?? 0);
        var firstHash = first >= 0 && first < graph.Count ? graph[first].Id.GetHashCode() : 0;
        var columns = ScreenPadding.Columns(listView);
        var width = PanePadding.TextWidth(listView.Viewport.Width, columns);
        var window = new JumpWindow(first, listView.Viewport.Height, cursor);

        return _jump.Labels(text, window, HashCode.Combine(graph.Count, firstHash, width), () => new GraphJumpRows(graph, OptionsFor(width)));
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
            : ShowsCutMarker && commitIndex == graph.Count
                ? GraphRowRenderer.RenderCutMarker(options, selected)
                : GraphRowRenderer.RenderEmpty(options, selected);
        LinePainter.Paint(
            listView,
            PanePadding.Wrap(line, columns),
            col,
            row,
            listView.HasFocus);
    }

    public void Dispose()
    {
    }

    // The labels are worked out once per frame (the first row asks), not once per row.
    private JumpLabel? JumpLabelOf(ListView listView, int commitIndex)
    {
        if (string.IsNullOrEmpty(JumpText))
        {
            return null;
        }

        JumpLabels(listView, JumpText);

        return _jump.LabelOf(commitIndex);
    }

    private GraphRowOptions OptionsFor(int width)
    {
        var laneColumns = GraphRowOptions.ChooseLaneColumns(graph.MaximumLaneCount, width);
        if (_cachedOptions is null
            || _cachedOptions.Width != width
            || _cachedOptions.LaneColumns != laneColumns
            || _cachedOptions.Highlight != Highlight
            || !ReferenceEquals(_cachedOptions.UpstreamGoneBranches, UpstreamGoneBranches))
        {
            _cachedOptions = new GraphRowOptions(width, laneColumns, glyphs, Highlight, UpstreamGoneBranches);
        }

        return _cachedOptions;
    }
}
