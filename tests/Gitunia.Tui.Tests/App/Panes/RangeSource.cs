using System.Collections;
using System.Collections.Specialized;
using Gitunia.Core.Lists;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Tests.App.Panes;

/// <summary>A list source of empty rows that holds a Visual range.</summary>
public sealed class RangeSource(int count) : IListDataSource, IRangeSource
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { }
        remove { }
    }

    public int Count => count;

    public int MaxItemLength => 0;

    public bool SuspendCollectionChangedEvent { get; set; }

    public RowRange? Range { get; set; }

    public event Action? RangeInvalidated;

    public void Invalidate() => RangeInvalidated?.Invoke();

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Array.Empty<object>();

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
    }

    public void Dispose()
    {
    }
}
