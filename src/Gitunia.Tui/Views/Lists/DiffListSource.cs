using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Diff;
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
    private DiffWrapMap? _map;
    private DiffSplit? _split;
    private DiffSplitMap? _splitMap;
    private int _paneWidth;
    private int _gutterDigits = DiffRowRenderer.GutterDigits(0);

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DiffDocument Document { get; private set; } = DiffDocument.Empty;

    /// <summary>The diff search text to highlight, null for none.</summary>
    public string? Highlight { get; set; }

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
        RangeInvalidated?.Invoke();
        _map = null;
        _splitMap = null;
        Notify();
    }

    /// <summary>Turns the side-by-side view on or off and tells the list.</summary>
    public void SetSideBySide(bool isSideBySide)
    {
        IsSideBySide = isSideBySide;
        RangeInvalidated?.Invoke();
        _splitMap = null;
        Notify();
    }

    /// <summary>The diff row shown on screen line <paramref name="line"/>.</summary>
    /// <remarks>Side by side, the first row of the line: the old side's when it has one.</remarks>
    public int RowOf(int line) => ShowsSideBySide ? SplitMap().RowOf(line) : Map() is { } current ? current.RowOf(line) : line;

    /// <summary>The last diff row shown on screen line <paramref name="line"/>: the new side's row of a side-by-side line, else the same as <see cref="RowOf"/>.</summary>
    public int LastRowOf(int line) => ShowsSideBySide ? SplitMap().LastRowOf(line) : RowOf(line);

    /// <summary>The first screen line of diff row <paramref name="row"/>.</summary>
    public int LineOf(int row) => ShowsSideBySide ? SplitMap().StartOf(row) : Map() is { } current ? current.StartOf(row) : row;

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
        var line = ShowsSideBySide
            ? SplitLine(item, width, selected)
            : current is not null && item >= 0 && item < current.Count
            ? WrappedLine(current, item, width, selected)
            : current is null && item >= 0 && item < Document.Rows.Count
                ? DiffRowRenderer.Render(Document.Rows[item], width, _gutterDigits, Highlight, selected, glyphs)
                : new LineBuilder(width, glyphs.Ellipsis).Build();
        LinePainter.Paint(listView, PanePadding.Wrap(line, columns), col, row, listView.HasFocus);
    }

    public void Dispose()
    {
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

    private StyledLine SplitLine(int item, int width, bool isSelected)
    {
        var current = SplitMap();
        if (item < 0 || item >= current.Count)
        {
            return new LineBuilder(width, glyphs.Ellipsis).Build();
        }

        var index = current.SplitLineOf(item);
        return DiffSplitRenderer.Render(
            Document,
            current.Split.Lines[index],
            item - current.StartOfSplitLine(index),
            width,
            _gutterDigits,
            Highlight,
            isSelected,
            IsWrapped,
            glyphs);
    }

    private StyledLine WrappedLine(DiffWrapMap current, int item, int width, bool isSelected)
    {
        var row = current.RowOf(item);
        return DiffRowRenderer.RenderSegment(Document.Rows[row], item - current.StartOf(row), width, _gutterDigits, Highlight, isSelected, glyphs);
    }

    private void Notify()
    {
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
