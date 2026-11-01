using System.Collections;
using System.Collections.Specialized;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>List source of a pane that only holds a note in its first row (a part of the Changes view that is not built yet).</summary>
public sealed class PlaceholderListSource(string text, GraphGlyphSet glyphs) : IListDataSource
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { }
        remove { }
    }

    public int Count => 1;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var padding = ScreenPadding.Columns(listView);
        var builder = new LineBuilder(PanePadding.TextWidth(width, padding), glyphs.Ellipsis);
        if (item == 0)
        {
            builder.Append(text, SegmentRole.Faint);
        }

        LinePainter.Paint(listView, PanePadding.Wrap(builder.Build(), padding), col, row, isFocused: false);
    }

    public void Dispose()
    {
    }
}
