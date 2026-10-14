using System.Text;

namespace Gitunia.CustomRenderTui;

/// <summary>Cell grid with diff-based flush: only changed cells are emitted, in one buffered write.</summary>
public sealed class CellBuffer
{
    private readonly StringBuilder _builder = new(1 << 16);
    private Cell[] _front = [];
    private Cell[] _back = [];
    private bool _forceFull = true;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Resize(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _front = new Cell[Width * Height];
        _back = new Cell[Width * Height];
        _forceFull = true;
    }

    public void Clear() => Array.Fill(_back, new Cell(' ', Style.None));

    public void Put(int x, int y, ReadOnlySpan<char> text, Style style, byte foreground = 0)
    {
        if ((uint)y >= (uint)Height)
        {
            return;
        }

        for (var i = 0; i < text.Length && x + i < Width; i++)
        {
            if (x + i >= 0)
            {
                var character = text[i];
                _back[(y * Width) + x + i] = new Cell(character < ' ' ? ' ' : character, style, foreground);
            }
        }
    }

    /// <summary>Draws a single-line box; the title is written into the top edge.</summary>
    public void Box(int x, int y, int width, int height, string title, Style style)
    {
        if (width < 2 || height < 2)
        {
            return;
        }

        Put(x, y, $"┌{new string('─', width - 2)}┐", style);
        Put(x, y + height - 1, $"└{new string('─', width - 2)}┘", style);
        for (var row = 1; row < height - 1; row++)
        {
            Put(x, y + row, "│", style);
            Put(x + width - 1, y + row, "│", style);
        }

        if (title.Length > 0 && width > 6)
        {
            var label = $" {(title.Length > width - 6 ? title[..(width - 6)] : title)} ";
            Put(x + 2, y, label, style);
        }
    }

    /// <summary>Adds a style to existing cells (used for selection highlight).</summary>
    public void StyleSpan(int x, int y, int width, Style style)
    {
        if ((uint)y >= (uint)Height)
        {
            return;
        }

        for (var i = Math.Max(0, x); i < Math.Min(Width, x + width); i++)
        {
            var cellIndex = (y * Width) + i;
            _back[cellIndex] = _back[cellIndex] with { Style = _back[cellIndex].Style | style };
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
        _builder.Clear();
        if (_forceFull)
        {
            _builder.Append("\e[2J");
        }

        var currentStyle = Style.None;
        byte currentForeground = 0;
        var cursorX = -1;
        var cursorY = -1;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var i = (y * Width) + x;
                var cell = _back[i];
                if (!_forceFull && cell == _front[i])
                {
                    continue;
                }

                if (cursorY != y || cursorX != x)
                {
                    _builder.Append("\e[").Append(y + 1).Append(';').Append(x + 1).Append('H');
                }

                if (cell.Style != currentStyle || cell.Foreground != currentForeground)
                {
                    _builder.Append("\e[0");
                    if ((cell.Style & Style.Reverse) != 0)
                    {
                        _builder.Append(";7");
                    }

                    if ((cell.Style & Style.Dim) != 0)
                    {
                        _builder.Append(";2");
                    }

                    if ((cell.Style & Style.Bold) != 0)
                    {
                        _builder.Append(";1");
                    }

                    if (cell.Foreground != 0)
                    {
                        _builder.Append(";38;5;").Append(cell.Foreground);
                    }

                    _builder.Append('m');
                    currentStyle = cell.Style;
                    currentForeground = cell.Foreground;
                }

                _builder.Append(cell.Character);
                cursorX = x + 1;
                cursorY = y;
            }
        }

        _forceFull = false;
        Array.Copy(_back, _front, _back.Length);
        if (_builder.Length == 0)
        {
            return [];
        }

        _builder.Append("\e[0m");
        return Encoding.UTF8.GetBytes(_builder.ToString());
    }
}
