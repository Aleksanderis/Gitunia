namespace Gitunia.CustomRenderTui.Demo.Graph;

/// <summary>
/// Incremental lane assignment for a newest-first commit stream. Each commit gets a row of glyph codes
/// (one per lane, capped at <see cref="MaxLanes"/>); the lane index doubles as the colour index.
/// Memory is one byte per drawn lane per commit.
/// </summary>
public sealed class LaneLayout
{
    public const int MaxLanes = 12;

    public const byte Empty = 0;
    public const byte Pipe = 1;
    public const byte Node = 2;
    public const byte Merge = 3; // a lane converging into this commit's lane
    public const byte Fork = 4; // a lane opened by this commit for a second parent

    private readonly List<ulong> _lanes = [];
    private byte[] _glyphs = new byte[1 << 16];
    private int[] _start = new int[1 << 14];
    private int _glyphLen;

    public int Count { get; private set; }

    public ReadOnlySpan<byte> Row(int i) => _glyphs.AsSpan(_start[i], _start[i + 1] - _start[i]);

    public void Add(ulong hash, ReadOnlySpan<ulong> parents)
    {
        var col = _lanes.IndexOf(hash);
        if (col < 0)
        {
            col = FreeLane();
        }

        Span<byte> marks = stackalloc byte[MaxLanes];
        for (var i = 0; i < _lanes.Count; i++)
        {
            if (i != col && _lanes[i] == hash)
            {
                if (i < MaxLanes)
                {
                    marks[i] = Merge;
                }

                _lanes[i] = 0;
            }
        }

        _lanes[col] = parents.Length > 0 ? parents[0] : 0;
        for (var p = 1; p < parents.Length; p++)
        {
            if (_lanes.Contains(parents[p]))
            {
                continue;
            }

            var slot = FreeLane(col);
            _lanes[slot] = parents[p];
            if (slot < MaxLanes)
            {
                marks[slot] = Fork;
            }
        }

        var width = Math.Min(_lanes.Count, MaxLanes);
        Ensure(width);
        for (var i = 0; i < width; i++)
        {
            _glyphs[_glyphLen++] = i == col ? Node : marks[i] != Empty ? marks[i] : _lanes[i] != 0 ? Pipe : Empty;
        }

        _start[++Count] = _glyphLen;
        while (_lanes.Count > 0 && _lanes[^1] == 0)
        {
            _lanes.RemoveAt(_lanes.Count - 1);
        }
    }

    private int FreeLane(int except = -1)
    {
        for (var i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i] == 0 && i != except)
            {
                return i;
            }
        }

        _lanes.Add(0);
        return _lanes.Count - 1;
    }

    private void Ensure(int width)
    {
        if (_glyphLen + width > _glyphs.Length)
        {
            Array.Resize(ref _glyphs, Math.Max(_glyphs.Length * 2, _glyphLen + width));
        }

        if (Count + 2 > _start.Length)
        {
            Array.Resize(ref _start, _start.Length * 2);
        }
    }
}
