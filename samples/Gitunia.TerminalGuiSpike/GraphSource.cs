using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

namespace Gitunia.Tui;

/// <summary>Per-row lane info: commit lane plus bit masks for pass-through, join (merge-in) and fork (extra parent) lanes.</summary>
public readonly record struct GraphRow(string Hash, string Subject, int Lane, ulong Through, ulong Join, ulong Fork, bool IsMerge);

/// <summary>Incremental lane assignment, fed in log (date) order.</summary>
public sealed class LaneBuilder
{
    private readonly List<string?> _lanes = new();

    public GraphRow Add(string hash, string[] parents, string subject)
    {
        var idx = _lanes.IndexOf(hash);
        if (idx < 0)
        {
            idx = FreeSlot();
            _lanes[idx] = hash;
        }

        ulong join = 0;
        for (var j = idx + 1; j < _lanes.Count; j++)
        {
            if (_lanes[j] == hash)
            {
                join |= Bit(j);
                _lanes[j] = null;
            }
        }

        ulong through = 0;
        for (var i = 0; i < _lanes.Count; i++)
        {
            if (i != idx && _lanes[i] is not null)
            {
                through |= Bit(i);
            }
        }

        ulong fork = 0;
        _lanes[idx] = parents.Length > 0 ? parents[0] : null;
        for (var p = 1; p < parents.Length; p++)
        {
            var k = _lanes.IndexOf(parents[p]);
            if (k < 0)
            {
                k = FreeSlot();
                _lanes[k] = parents[p];
            }

            fork |= Bit(k);
        }

        while (_lanes.Count > 0 && _lanes[^1] is null)
        {
            _lanes.RemoveAt(_lanes.Count - 1);
        }

        return new GraphRow(hash, subject, idx, through, join, fork, parents.Length > 1);
    }

    private static ulong Bit(int i) => i < 64 ? 1UL << i : 0;

    private int FreeSlot()
    {
        var i = _lanes.IndexOf(null);
        if (i >= 0)
        {
            return i;
        }

        _lanes.Add(null);
        return _lanes.Count - 1;
    }
}

/// <summary>Append-only list source drawing a coloured commit graph, then "hash subject".</summary>
public sealed class GraphSource : IListDataSource
{
    public const int GraphLanes = 12;

    private static readonly Color[] Palette =
    [
        Color.BrightRed, Color.BrightGreen, Color.BrightYellow, Color.BrightBlue, Color.BrightMagenta, Color.BrightCyan,
    ];

    public List<GraphRow> Rows { get; } = new();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => Rows.Count;

    public int MaxItemLength => 200;

    public bool SuspendCollectionChangedEvent { get; set; }

    public void AddRange(List<GraphRow> batch)
    {
        Rows.AddRange(batch);
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => Rows;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        listView.Move(col, row);
        var baseAttr = listView.GetAttributeForRole(selected ? VisualRole.Focus : VisualRole.Normal);
        if (item >= Rows.Count)
        {
            listView.SetAttribute(baseAttr);
            listView.AddStr(new string(' ', width));
            return;
        }

        var r = Rows[item];
        var used = 0;
        var span = r.Join | r.Fork;
        var high = span == 0 ? -1 : 63 - BitOperations.LeadingZeroCount(span);
        for (var i = 0; i < GraphLanes; i++)
        {
            var ch = ' ';
            var tail = ' ';
            var bit = 1UL << i;
            if (i == r.Lane)
            {
                ch = r.IsMerge ? '◉' : '●';
            }
            else if ((r.Through & bit) != 0)
            {
                ch = '│';
            }
            else if ((r.Join & bit) != 0)
            {
                ch = '╯';
            }
            else if ((r.Fork & bit) != 0)
            {
                ch = '╮';
            }

            if (ch != ' ' && ch != '│' && ch != '╯' && ch != '╮' && i < high)
            {
                tail = '─';
            }

            listView.SetAttribute(new Terminal.Gui.Drawing.Attribute(Palette[i % Palette.Length], baseAttr.Background));
            listView.AddStr(string.Create(2, (ch, tail), static (s, st) =>
            {
                s[0] = st.ch;
                s[1] = st.tail;
            }));
            used += 2;
        }

        listView.SetAttribute(baseAttr);
        var text = r.Hash[..7] + " " + r.Subject;
        var avail = Math.Max(0, width - used);
        if (text.Length > avail)
        {
            text = text[..avail];
        }

        listView.AddStr(text.PadRight(avail));
    }

    public void Dispose()
    {
    }
}
