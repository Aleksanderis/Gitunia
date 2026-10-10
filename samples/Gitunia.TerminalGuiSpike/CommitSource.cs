using System.Collections;
using System.Collections.Specialized;
using Terminal.Gui.Views;

namespace Gitunia.Tui;

/// <summary>Append-only list data source; avoids ListWrapper's per-add O(n) max-length scan.</summary>
public sealed class CommitSource : IListDataSource
{
    public List<string> Items { get; } = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => Items.Count;

    public int MaxItemLength => 200;

    public bool SuspendCollectionChangedEvent { get; set; }

    public void AddRange(List<string> batch)
    {
        Items.AddRange(batch);
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Items;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        listView.Move(col, row);
        var s = item < Items.Count ? Items[item] : "";
        if (viewportX < s.Length)
        {
            s = s[viewportX..];
        }
        else
        {
            s = "";
        }

        if (s.Length > width)
        {
            s = s[..width];
        }

        listView.AddStr(s.PadRight(width));
    }

    public void Dispose()
    {
    }
}
