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
    public const byte Merge = 3; // A lane converging into this commit's lane
    public const byte Fork = 4; // A lane opened by this commit for a second parent

    private readonly List<ulong> _lanes = [];
    private byte[] _glyphs = new byte[1 << 16];
    private int[] _starts = new int[1 << 14];
    private int _glyphLength;

    public int Count { get; private set; }

    public ReadOnlySpan<byte> Row(int index) => _glyphs.AsSpan(_starts[index], _starts[index + 1] - _starts[index]);

    public void Add(ulong hash, ReadOnlySpan<ulong> parents)
    {
        var column = _lanes.IndexOf(hash);
        if (column < 0)
        {
            column = FreeLane();
        }

        Span<byte> marks = stackalloc byte[MaxLanes];
        for (var i = 0; i < _lanes.Count; i++)
        {
            if (i != column && _lanes[i] == hash)
            {
                if (i < MaxLanes)
                {
                    marks[i] = Merge;
                }

                _lanes[i] = 0;
            }
        }

        _lanes[column] = parents.Length > 0 ? parents[0] : 0;
        for (var parentIndex = 1; parentIndex < parents.Length; parentIndex++)
        {
            if (_lanes.Contains(parents[parentIndex]))
            {
                continue;
            }

            var slot = FreeLane(column);
            _lanes[slot] = parents[parentIndex];
            if (slot < MaxLanes)
            {
                marks[slot] = Fork;
            }
        }

        var width = Math.Min(_lanes.Count, MaxLanes);
        Ensure(width);
        for (var i = 0; i < width; i++)
        {
            _glyphs[_glyphLength++] = i == column ? Node : marks[i] != Empty ? marks[i] : _lanes[i] != 0 ? Pipe : Empty;
        }

        _starts[++Count] = _glyphLength;
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
        if (_glyphLength + width > _glyphs.Length)
        {
            Array.Resize(ref _glyphs, Math.Max(_glyphs.Length * 2, _glyphLength + width));
        }

        if (Count + 2 > _starts.Length)
        {
            Array.Resize(ref _starts, _starts.Length * 2);
        }
    }
}
