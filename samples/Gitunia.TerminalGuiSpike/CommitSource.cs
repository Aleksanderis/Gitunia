using System.Collections;
using System.Collections.Specialized;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Gitunia.Tui;

/// <summary>Sidebar row: a section header or a ref name.</summary>
public readonly record struct SidebarItem(string Text, bool Header);

/// <summary>Append-only sidebar data source (avoids ListWrapper's per-add O(n) scan); headers are bold and blue.</summary>
public sealed class CommitSource : IListDataSource
{
    public List<SidebarItem> Items { get; } = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => Items.Count;

    public int MaxItemLength => 200;

    public bool SuspendCollectionChangedEvent { get; set; }

    public void AddRange(List<SidebarItem> batch)
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
        var entry = item < Items.Count ? Items[item] : default;
        var text = entry.Text is null ? "" : (entry.Header ? "▾ " : "  ") + entry.Text;
        if (text.Length > width)
        {
            text = text[..width];
        }

        var style = entry.Header ? TextStyle.Bold : TextStyle.None;
        if (selected && listView.HasFocus)
        {
            style |= TextStyle.Reverse;
        }

        var foreground = entry.Header ? Look.SectionBlue : Color.None;
        listView.SetAttribute(new Attribute(foreground, Color.None, style));
        listView.AddStr(text.PadRight(width));
    }

    public void Dispose()
    {
    }
}
