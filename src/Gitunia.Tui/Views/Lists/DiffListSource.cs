using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Diff;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Lists;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>List source over the rows of a <see cref="DiffDocument"/>: only the visible rows are drawn, so a 50k-line diff costs no more per frame than a small one.</summary>
public sealed class DiffListSource(GraphGlyphSet glyphs) : IListDataSource, IRangeSource
{
    private readonly JumpLabelCache _jump = new();
    private DiffWrapMap? _map;
    private DiffSplit? _split;
    private DiffSplitMap? _splitMap;
    private int _paneWidth;
    private DiffDocument? _groupDocument;
    private int _groupCursor = -1;
    private RowRange? _group;
    private int _gutterDigits = DiffRowRenderer.GutterDigits(0);

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DiffDocument Document { get; private set; } = DiffDocument.Empty;

    /// <summary>The change group under the cursor is marked (the working-tree diff, where a group is the unit of staging).</summary>
    public bool HighlightsGroup { get; set; }

    /// <summary>Which staging buttons the first line of the highlighted change group carries (none by default); read on every draw, as the side follows the selected file.</summary>
    public Func<WorkingTreeButtons> GroupButtons { get; set; } = () => WorkingTreeButtons.None;

    /// <summary>The diff search text to highlight, null for none.</summary>
    public string? Highlight { get; set; }

    /// <summary>The text of an open quick jump, whose targets in view get labels; null when none is open.</summary>
    public string? JumpText
    {
        get => _jump.Text;
        set => _jump.Text = value;
    }

    /// <summary>The pane width in columns, set by the controller on every layout change; wrapping needs it. Zero shows the rows unwrapped.</summary>
    public int Width
    {
        get => _paneWidth;
        set
        {
            if (_paneWidth != value && (IsWrapped || IsSideBySide))
            {
                RangeInvalidated?.Invoke();
            }

            _jump.Invalidate();
            _paneWidth = value;
        }
    }

    /// <summary>Text wraps over several screen lines instead of being cut.</summary>
    public bool IsWrapped { get; private set; }

    /// <summary>The side-by-side view is switched on (it shows only while the pane is wide enough, see <see cref="ShowsSideBySide"/>).</summary>
    public bool IsSideBySide { get; private set; }

    /// <summary>Rows are drawn as old and new side by side: switched on and the pane has the <see cref="DiffSplitRenderer.MinimumWidth"/> columns it needs.</summary>
    public bool ShowsSideBySide => IsSideBySide && Width >= DiffSplitRenderer.MinimumWidth;

    /// <summary>Screen lines: one per row, or fewer or more when side by side or wrapped.</summary>
    public int Count => ShowsSideBySide ? SplitMap().Count : Map()?.Count ?? Document.Rows.Count;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    public RowRange? Range { get; set; }

    public event Action? RangeInvalidated;

    /// <summary>Shows another document and tells the list.</summary>
    public void Update(DiffDocument document)
    {
        Document = document;
        _jump.Invalidate();
        RangeInvalidated?.Invoke();
        _map = null;
        _split = null;
        _splitMap = null;
        _gutterDigits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        Notify();
    }

    /// <summary>Turns wrapping on or off and tells the list.</summary>
    public void SetWrapped(bool isWrapped)
    {
        IsWrapped = isWrapped;
        _jump.Invalidate();
        RangeInvalidated?.Invoke();
        _map = null;
        _splitMap = null;
        Notify();
    }

    /// <summary>Turns the side-by-side view on or off and tells the list.</summary>
    public void SetSideBySide(bool isSideBySide)
    {
        IsSideBySide = isSideBySide;
        _jump.Invalidate();
        RangeInvalidated?.Invoke();
        _splitMap = null;
        Notify();
    }

    /// <summary>The diff row shown on screen line <paramref name="line"/>.</summary>
    /// <remarks>Side by side, the first row of the line: the old side's when it has one.</remarks>
    public int RowOf(int line)
    {
        if (ShowsSideBySide)
        {
            return SplitMap().RowOf(line);
        }

        return Map() is { } current ? current.RowOf(line) : line;
    }

    /// <summary>The last diff row shown on screen line <paramref name="line"/>: the new side's row of a side-by-side line, else the same as <see cref="RowOf"/>.</summary>
    public int LastRowOf(int line) => ShowsSideBySide ? SplitMap().LastRowOf(line) : RowOf(line);

    /// <summary>The first screen line of diff row <paramref name="row"/>.</summary>
    public int LineOf(int row)
    {
        if (ShowsSideBySide)
        {
            return SplitMap().StartOf(row);
        }

        return Map() is { } current ? current.StartOf(row) : row;
    }

    /// <summary>
    /// The quick-jump targets of <paramref name="text"/> among the diff rows in view of <paramref name="listView"/>, nearest the
    /// cursor first. A row with wrapped lines is in view when any of its lines is.
    /// </summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="text">The typed text.</param>
    public IReadOnlyList<JumpLabel> JumpLabels(ListView listView, string text)
    {
        var width = PanePadding.TextWidth(listView.Viewport.Width, ScreenPadding.Columns(listView));
        if (Count == 0)
        {
            return [];
        }

        var firstLine = Math.Clamp(listView.Viewport.Y, 0, Count - 1);
        var lastLine = Math.Clamp(listView.Viewport.Y + listView.Viewport.Height - 1, firstLine, Count - 1);
        var first = RowOf(firstLine);
        if (LineOf(first) < firstLine)
        {
            // A wrapped row scrolled partly off the top shows no first line, so it has no room for a label.
            first++;
        }

        var cursor = RowOf(Math.Clamp(listView.SelectedItem ?? 0, 0, Count - 1));
        if (ShowsSideBySide)
        {
            return SplitJumpLabels(text, firstLine, lastLine, cursor, width);
        }

        var window = new JumpWindow(first, LastRowOf(lastLine) - first + 1, cursor);

        return _jump.Labels(text, window, width, () => new DiffJumpRows(Document, width, _gutterDigits, glyphs.Ellipsis));
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var columns = ScreenPadding.Columns(listView);
        var current = Map();
        width = PanePadding.TextWidth(width, columns);
        selected |= Range?.Contains(item) == true;
        var jump = ShowsSideBySide ? null : JumpLabelAt(listView, current, item);
        var line = ShowsSideBySide
            ? SplitLine(listView, item, width, selected)
            : current is not null && item >= 0 && item < current.Count
            ? WrappedLine(current, item, width, selected, jump)
            : current is null && item >= 0 && item < Document.Rows.Count
                ? DiffRowRenderer.Render(Document.Rows[item], width, _gutterDigits, Highlight, selected, glyphs, jump)
                : new LineBuilder(width, glyphs.Ellipsis).Build();
        var padded = PanePadding.Wrap(line, columns);
        if (IsInCursorGroup(listView, item))
        {
            if (GroupButtonsAt(item, columns, width) is { Count: > 0 } buttons)
            {
                padded = ChangeGroupButtons.Overlay(padded, buttons, columns, glyphs);
            }

            padded = ChangeGroupMarker.Mark(padded, glyphs);
        }

        LinePainter.Paint(listView, padded, col, row, listView.HasFocus);
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// The view of the group button under the pointer at <paramref name="column"/>, <paramref name="row"/> (inside <paramref name="listView"/>),
    /// or null when there is none there: the buttons sit on the first line of the change group under the cursor only.
    /// </summary>
    public string? ButtonAt(ListView listView, int column, int row)
    {
        var item = listView.Viewport.Y + row;
        var columns = ScreenPadding.Columns(listView);
        var width = PanePadding.TextWidth(listView.Viewport.Width, columns);

        return IsInCursorGroup(listView, item) && GroupButtonsAt(item, columns, width) is { } buttons
            ? TitleButton.ViewAt(buttons, column + listView.Viewport.X)
            : null;
    }

    /// <summary>
    /// Whether screen line <paramref name="item"/> is in the change group under the cursor; never while a Visual range is open
    /// (the range is then what the keys act on) or when <see cref="HighlightsGroup"/> is off.
    /// </summary>
    public bool IsInCursorGroup(ListView listView, int item)
    {
        if (!HighlightsGroup || Range is not null || Count == 0 || item < 0 || item >= Count)
        {
            return false;
        }

        var cursor = RowOf(Math.Clamp(listView.SelectedItem ?? 0, 0, Count - 1));
        if (!ReferenceEquals(_groupDocument, Document) || _groupCursor != cursor)
        {
            _groupDocument = Document;
            _groupCursor = cursor;
            _group = DiffSelection.GroupRowsAt(Document, cursor);
        }

        return _group is { } group && RowOf(item) <= group.Last && LastRowOf(item) >= group.First;
    }

    // The buttons for screen line item, which must be in the cursor's group: only its first line has them.
    private IReadOnlyList<TitleButton>? GroupButtonsAt(int item, int columns, int width)
    {
        if (_group is not { } group || item != LineOf(group.First))
        {
            return null;
        }

        return ChangeGroupButtons.Layout(width, columns, GroupButtons());
    }

    private DiffWrapMap? Map()
    {
        var width = IsWrapped ? Width : 0;
        if (width <= 0)
        {
            return null;
        }

        if (_map is null || !ReferenceEquals(_map.Document, Document))
        {
            _map = DiffWrapMap.Build(Document, width);
        }
        else if (_map.Width != width)
        {
            _map = _map.Resize(width);
        }

        return _map;
    }

    private DiffSplitMap SplitMap()
    {
        _split ??= DiffSplitLayout.Build(Document);
        if (_splitMap is null || _splitMap.IsWrapped != IsWrapped)
        {
            _splitMap = DiffSplitMap.Build(Document, _split, Width, IsWrapped);
        }
        else if (_splitMap.Width != Width)
        {
            _splitMap = _splitMap.Resize(Width);
        }

        return _splitMap;
    }

    private StyledLine SplitLine(ListView listView, int item, int width, bool isSelected)
    {
        var current = SplitMap();
        if (item < 0 || item >= current.Count)
        {
            return new LineBuilder(width, glyphs.Ellipsis).Build();
        }

        var index = current.SplitLineOf(item);
        var segment = item - current.StartOfSplitLine(index);
        var splitLine = current.Split.Lines[index];
        JumpLabel? left = null;
        JumpLabel? right = null;
        if (segment == 0 && !string.IsNullOrEmpty(JumpText))
        {
            JumpLabels(listView, JumpText);
            left = _jump.LabelOf(splitLine.LeftRow);
            var sharesRow = splitLine.IsFull || splitLine.LeftRow == splitLine.RightRow;
            right = sharesRow ? null : _jump.LabelOf(splitLine.RightRow);
        }

        return DiffSplitRenderer.Render(
            Document,
            current.Split.Lines[index],
            segment,
            width,
            _gutterDigits,
            Highlight,
            isSelected,
            IsWrapped,
            glyphs,
            left,
            right);
    }

    private StyledLine WrappedLine(DiffWrapMap current, int item, int width, bool isSelected, JumpLabel? jump)
    {
        var row = current.RowOf(item);
        return DiffRowRenderer.RenderSegment(Document.Rows[row], item - current.StartOf(row), width, _gutterDigits, Highlight, isSelected, glyphs, jump);
    }

    // The labels are worked out once per frame (the first line asks); only the first line of a wrapped row shows its label.
    private JumpLabel? JumpLabelAt(ListView listView, DiffWrapMap? current, int item)
    {
        if (string.IsNullOrEmpty(JumpText) || item < 0 || item >= Count)
        {
            return null;
        }

        var row = current is null ? item : current.RowOf(item);
        if (current is not null && current.StartOf(row) != item)
        {
            return null;
        }

        JumpLabels(listView, JumpText);

        return _jump.LabelOf(row);
    }

    // Side by side the rows of a line sit in two halves: the window spans the rows of the lines in view, and rows between
    // that are on lines out of view get no text (DiffJumpRows), so no label is hidden off screen.
    private IReadOnlyList<JumpLabel> SplitJumpLabels(string text, int firstLine, int lastLine, int cursor, int width)
    {
        var map = SplitMap();
        var first = map.SplitLineOf(firstLine);
        if (map.StartOfSplitLine(first) < firstLine)
        {
            first++;
        }

        var last = map.SplitLineOf(lastLine);
        if (first > last)
        {
            return [];
        }

        var low = int.MaxValue;
        var high = -1;
        for (var index = first; index <= last; index++)
        {
            var line = map.Split.Lines[index];
            low = Math.Min(low, line.FirstRow);
            high = Math.Max(high, line.LastRow);
        }

        var window = new JumpWindow(low, high - low + 1, cursor);
        var stamp = HashCode.Combine(width, first, last, IsWrapped);
        return _jump.Labels(
            text,
            window,
            stamp,
            () => new DiffJumpRows(Document, width, _gutterDigits, glyphs.Ellipsis, map.Split, first, last));
    }

    private void Notify()
    {
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
