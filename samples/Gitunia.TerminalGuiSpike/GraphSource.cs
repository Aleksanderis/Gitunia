using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

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

/// <summary>Append-only list source drawing a coloured commit graph, then a dimmed short hash and the subject.</summary>
public sealed class GraphSource : IListDataSource
{
    public const int MaxLanes = 12;

    private (int Top, int Height, int Count, int Width) _laneCache = (-1, -1, -1, 1);

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

    /// <summary>Lane columns needed by the rows currently on screen (so short histories get no wide gap).</summary>
    private int LaneWidth(ListView lv)
    {
        var key = (lv.Viewport.Y, lv.Viewport.Height, Rows.Count);
        if (_laneCache.Top == key.Item1 && _laneCache.Height == key.Item2 && _laneCache.Count == key.Item3)
        {
            return _laneCache.Width;
        }

        var lanes = 1;
        for (var i = key.Item1; i < Math.Min(Rows.Count, key.Item1 + key.Item2); i++)
        {
            var r = Rows[i];
            var all = r.Through | r.Join | r.Fork | (1UL << r.Lane);
            lanes = Math.Max(lanes, 64 - BitOperations.LeadingZeroCount(all));
        }

        lanes = Math.Min(lanes, MaxLanes);
        _laneCache = (key.Item1, key.Item2, key.Item3, lanes);
        return lanes;
    }

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        listView.Move(col, row);
        var rev = selected && listView.HasFocus ? TextStyle.Reverse : TextStyle.None;
        if (item >= Rows.Count)
        {
            listView.SetAttribute(new Attribute(Color.None, Color.None, TextStyle.None));
            listView.AddStr(new string(' ', width));
            return;
        }

        var r = Rows[item];
        var lanes = LaneWidth(listView);
        var used = 0;
        for (var i = 0; i < lanes; i++)
        {
            var ch = ' ';
            var bit = 1UL << i;
            if (i == r.Lane)
            {
                ch = '●';
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

            listView.SetAttribute(new Attribute(Look.Lanes[i % Look.Lanes.Length], Color.None, rev));
            listView.AddStr(ch + " ");
            used += 2;
        }

        listView.SetAttribute(new Attribute(Look.HashGrey, Color.None, rev | TextStyle.Faint));
        listView.AddStr(" " + r.Hash[..7] + " ");
        used += 9;

        listView.SetAttribute(new Attribute(Color.None, Color.None, rev));
        var avail = Math.Max(0, width - used);
        var text = r.Subject.Length > avail ? r.Subject[..avail] : r.Subject;
        listView.AddStr(text.PadRight(avail));
    }

    public void Dispose()
    {
    }
}
