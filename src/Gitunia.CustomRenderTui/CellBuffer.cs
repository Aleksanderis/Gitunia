using System.Text;

namespace Gitunia.CustomRenderTui;

[Flags]
public enum Style : byte
{
    None = 0,
    Reverse = 1,
    Dim = 2,
    Bold = 4,
}

/// <summary>One terminal cell. Fg is a 256-colour palette index; 0 means the terminal default.</summary>
public readonly record struct Cell(char Ch, Style Style, byte Fg = 0);

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

    public void Put(int x, int y, ReadOnlySpan<char> text, Style style, byte fg = 0)
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
                _back[(y * Width) + x + i] = new Cell(c < ' ' ? ' ' : c, style, fg);
            }
        }
    }

    /// <summary>Draws a single-line box; the title is written into the top edge.</summary>
    public void Box(int x, int y, int w, int h, string title, Style style)
    {
        if (w < 2 || h < 2)
        {
            return;
        }

        Put(x, y, "┌" + new string('─', w - 2) + "┐", style);
        Put(x, y + h - 1, "└" + new string('─', w - 2) + "┘", style);
        for (var r = 1; r < h - 1; r++)
        {
            Put(x, y + r, "│", style);
            Put(x + w - 1, y + r, "│", style);
        }

        if (title.Length > 0 && w > 6)
        {
            var t = " " + (title.Length > w - 6 ? title[..(w - 6)] : title) + " ";
            Put(x + 2, y, t, style);
        }
    }

    /// <summary>Adds a style to existing cells (used for selection highlight).</summary>
    public void StyleSpan(int x, int y, int w, Style style)
    {
        if ((uint)y >= (uint)Height)
        {
            return;
        }

        for (var i = Math.Max(0, x); i < Math.Min(Width, x + w); i++)
        {
            var k = (y * Width) + i;
            _back[k] = _back[k] with { Style = _back[k].Style | style };
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
        byte curFg = 0;
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

                if (b.Style != cur || b.Fg != curFg)
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

                    if (b.Fg != 0)
                    {
                        _sb.Append(";38;5;").Append(b.Fg);
                    }

                    _sb.Append('m');
                    cur = b.Style;
                    curFg = b.Fg;
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
