using System.Text;

namespace Gitunia.Tui.Render;

[Flags]
public enum Style : byte
{
    None = 0,
    Reverse = 1,
    Dim = 2,
    Bold = 4,
}

public readonly record struct Cell(char Ch, Style Style);

/// <summary>Cell grid with diff-based flush: only changed cells are emitted, in one buffered write.</summary>
public sealed class CellBuffer
{
    private readonly StringBuilder _sb = new(1 << 16);
    private Cell[] _front = [];
    private Cell[] _back = [];
    private bool _forceFull = true;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Resize(int w, int h)
    {
        Width = Math.Max(1, w);
        Height = Math.Max(1, h);
        _front = new Cell[Width * Height];
        _back = new Cell[Width * Height];
        _forceFull = true;
    }

    public void Clear() => Array.Fill(_back, new Cell(' ', Style.None));

    public void Put(int x, int y, ReadOnlySpan<char> text, Style style)
    {
        if ((uint)y >= (uint)Height)
        {
            return;
        }

        for (var i = 0; i < text.Length && x + i < Width; i++)
        {
            if (x + i >= 0)
            {
                var c = text[i];
                _back[(y * Width) + x + i] = new Cell(c < ' ' ? ' ' : c, style);
            }
        }
    }

    public void StyleRow(int y, Style style)
    {
        for (var x = 0; x < Width; x++)
        {
            var i = (y * Width) + x;
            _back[i] = _back[i] with { Style = style };
        }
    }

    /// <summary>Returns the escape-sequence diff against the previous frame (empty when nothing changed).</summary>
    public byte[] Flush()
    {
        _sb.Clear();
        if (_forceFull)
        {
            _sb.Append("\e[2J");
        }

        var cur = Style.None;
        var curX = -1;
        var curY = -1;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var i = (y * Width) + x;
                var b = _back[i];
                if (!_forceFull && b == _front[i])
                {
                    continue;
                }

                if (curY != y || curX != x)
                {
                    _sb.Append("\e[").Append(y + 1).Append(';').Append(x + 1).Append('H');
                }

                if (b.Style != cur)
                {
                    _sb.Append("\e[0");
                    if ((b.Style & Style.Reverse) != 0)
                    {
                        _sb.Append(";7");
                    }

                    if ((b.Style & Style.Dim) != 0)
                    {
                        _sb.Append(";2");
                    }

                    if ((b.Style & Style.Bold) != 0)
                    {
                        _sb.Append(";1");
                    }

                    _sb.Append('m');
                    cur = b.Style;
                }

                _sb.Append(b.Ch);
                curX = x + 1;
                curY = y;
            }
        }

        _forceFull = false;
        Array.Copy(_back, _front, _back.Length);
        if (_sb.Length == 0)
        {
            return [];
        }

        _sb.Append("\e[0m");
        return Encoding.UTF8.GetBytes(_sb.ToString());
    }
}
