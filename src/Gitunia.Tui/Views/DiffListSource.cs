using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views;

/// <summary>List source over the rows of a <see cref="DiffDocument"/>: only the visible rows are drawn, so a 50k-line diff costs no more per frame than a small one.</summary>
public sealed class DiffListSource(GraphGlyphSet glyphs) : IListDataSource
{
    private int gutterDigits = DiffRowRenderer.GutterDigits(0);

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DiffDocument Document { get; private set; } = DiffDocument.Empty;

    /// <summary>The diff search text to highlight, null for none.</summary>
    public string? Highlight { get; set; }

    public int Count => Document.Rows.Count;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    /// <summary>Shows another document and tells the list.</summary>
    public void Update(DiffDocument document)
    {
        Document = document;
        gutterDigits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
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
        var line = item >= 0 && item < Document.Rows.Count
            ? DiffRowRenderer.Render(Document.Rows[item], width, gutterDigits, Highlight, selected && listView.HasFocus, glyphs)
            : new LineBuilder(width, glyphs.Ellipsis).Build();
        LinePainter.Paint(listView, line, col, row, listView.HasFocus);
    }

    public void Dispose()
    {
    }
}
