using System.Collections;
using System.Collections.Specialized;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>
/// The editor rows of the working-tree message box: one row per line of the visible window of the message, drawn by
/// <see cref="MessageBoxRenderer"/>. The view shows the draft of the active tab; the whole window is drawn once per redraw (on the
/// first row) and the other rows pick their line from it.
/// </summary>
/// <param name="port">The draft, cursor and committing facts of the active tab.</param>
/// <param name="glyphs">The character set.</param>
public sealed class MessageBoxListSource(
    IMessageBoxPort port,
    GraphGlyphSet glyphs) : IListDataSource
{
    // More rows than any terminal shows; the view only draws the visible ones.
    private const int RowCount = 512;

    private IReadOnlyList<StyledLine> _window = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { }
        remove { }
    }

    public int Count => RowCount;

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
        if (item == 0)
        {
            var rect = new PaneRect(0, 0, width, listView.Viewport.Height);
            _window = MessageBoxRenderer.EditorLines(port.Draft, rect, padding, listView.HasFocus && port.ShowsCursor, glyphs, port.IsCommitting);
        }

        if (item >= 0 && item < _window.Count)
        {
            LinePainter.Paint(listView, _window[item], col, row, isFocused: false);
        }
    }

    public void Dispose()
    {
    }
}
