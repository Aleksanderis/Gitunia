using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Lists;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views;

/// <summary>List source over the rows of the detail pane; the whole content is replaced when another commit is selected or its details arrive.</summary>
public sealed class DetailListSource(GraphGlyphSet glyphs) : IListDataSource, IRangeSource
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DetailContent Content { get; private set; } = DetailContent.Empty;

    public int Count => Content.Rows.Count;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    public RowRange? Range { get; set; }

    public event Action? RangeInvalidated;

    /// <summary>Shows other content and tells the list.</summary>
    public void Update(DetailContent content)
    {
        Content = content;
        RangeInvalidated?.Invoke();
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
        width = PanePadding.TextWidth(width);
        var line = item >= 0 && item < Content.Rows.Count
            ? DetailPaneRenderer.RenderRow(Content.Rows[item], selected || Range?.Contains(item) == true, width, glyphs)
            : DetailPaneRenderer.RenderEmpty(width, glyphs);
        LinePainter.Paint(listView, DetailPaneRenderer.OnPanel(PanePadding.Wrap(line)), col, row, listView.HasFocus);
    }

    public void Dispose()
    {
    }
}
