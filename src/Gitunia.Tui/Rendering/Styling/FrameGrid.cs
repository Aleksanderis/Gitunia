using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// The lines between and around the panes. Every pane is a box and boxes share their edges, so the corners and the T and cross
/// junctions come out of one rule: each cell remembers which sides a line leaves through and picks its character from that
/// (rounded outer corners, plain junctions, ASCII <c>+ - |</c> as fallback). The edges of the focused pane are drawn in the accent
/// colour, the others in the quiet border colour. The sidebar and graph titles sit on the top edge; the detail and diff titles are
/// built by their own renderers (they hold tabs and buttons) and use <see cref="SegmentRole.Rule"/> for the stretches of edge between
/// their texts, which <see cref="ResolveRules"/> turns into the cells of this grid. Pure data, so the views and the text frame draw the same lines.
/// </summary>
public sealed class FrameGrid
{
    private readonly int _width;
    private readonly int _height;
    private readonly GraphGlyphSet _glyphs;
    private readonly LineDirections[] _connections;
    private readonly bool[] _focusedCells;
    private readonly Dictionary<int, StyledSegment> _titleCells = [];

    private FrameGrid(int width, int height, GraphGlyphSet glyphs)
    {
        _width = Math.Max(0, width);
        _height = Math.Max(0, height);
        _glyphs = glyphs;
        _connections = new LineDirections[_width * _height];
        _focusedCells = new bool[_width * _height];
    }

    /// <summary>The grid of a screen.</summary>
    /// <param name="width">Terminal columns.</param>
    /// <param name="height">Terminal rows.</param>
    /// <param name="rects">The panes, as <see cref="PaneLayout"/> computed them.</param>
    /// <param name="focused">The focused pane, null for none.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="sidebarTitle">Title on the sidebar's top edge, empty for none.</param>
    /// <param name="graphTitle">Title on the graph's top edge, empty for none.</param>
    public static FrameGrid Build(int width, int height, PaneRects rects, PaneKind? focused, GraphGlyphSet glyphs, string sidebarTitle, string graphTitle)
    {
        var grid = new FrameGrid(width, height, glyphs);
        var sidebar = new PaneRect(rects.Sidebar.X, rects.Sidebar.Y, rects.Sidebar.Width, rects.Sidebar.Height + rects.SidebarFilter.Height);
        grid.AddBox(sidebar, focused == PaneKind.Sidebar, sidebarTitle);
        grid.AddBox(rects.Graph, focused == PaneKind.Graph, graphTitle);
        grid.AddBox(rects.Detail, focused == PaneKind.Detail, string.Empty);
        grid.AddBox(rects.Diff, focused == PaneKind.Diff, string.Empty);
        return grid;
    }

    /// <summary>The grid of a screen made of free boxes (the Changes view), which share the edge between neighbours.</summary>
    /// <param name="width">Terminal columns.</param>
    /// <param name="height">Terminal rows.</param>
    /// <param name="boxes">The panes.</param>
    /// <param name="glyphs">The character set.</param>
    public static FrameGrid Build(int width, int height, IReadOnlyList<FrameBox> boxes, GraphGlyphSet glyphs)
    {
        var grid = new FrameGrid(width, height, glyphs);
        foreach (var box in boxes)
        {
            grid.AddBox(box.Content, box.IsFocused, box.Title);
        }

        return grid;
    }

    /// <summary>The cell at a position: a line character, a title character, or a space where the grid has nothing.</summary>
    public StyledSegment Cell(int column, int row)
    {
        if (column < 0 || column >= _width || row < 0 || row >= _height)
        {
            return new StyledSegment(" ", SegmentRole.Plain, Selection: SegmentSelection.None);
        }

        var index = row * _width + column;
        if (_titleCells.TryGetValue(index, out var title))
        {
            return title;
        }

        var directions = _connections[index];
        if (directions == LineDirections.None)
        {
            return new StyledSegment(" ", SegmentRole.Plain, Selection: SegmentSelection.None);
        }

        var role = _focusedCells[index] ? SegmentRole.FocusedBorder : SegmentRole.Border;
        return new StyledSegment(Glyph(directions).ToString(), role, Selection: SegmentSelection.None);
    }

    /// <summary>The cells of one row from <paramref name="column"/>, <paramref name="count"/> of them, as a line.</summary>
    public StyledLine Row(int row, int column, int count)
    {
        var cells = new List<StyledSegment>(count);
        for (var offset = 0; offset < count; offset++)
        {
            Append(cells, Cell(column + offset, row));
        }

        return new StyledLine(cells);
    }

    /// <summary>Replaces the <see cref="SegmentRole.Rule"/> stretches of a title line (which starts at <paramref name="startColumn"/> of <paramref name="row"/>) by the grid's own cells.</summary>
    public StyledLine ResolveRules(StyledLine line, int row, int startColumn)
    {
        var segments = new List<StyledSegment>(line.Segments.Count);
        var column = startColumn;
        foreach (var segment in line.Segments)
        {
            var segmentWidth = TextWidth.Of(segment.Text);
            if (segment.Role == SegmentRole.Rule)
            {
                for (var offset = 0; offset < segmentWidth; offset++)
                {
                    Append(segments, Cell(column + offset, row) is { Role: SegmentRole.Border or SegmentRole.FocusedBorder } cell ? cell : new StyledSegment(_glyphs.Horizontal.ToString(), SegmentRole.Border, Selection: SegmentSelection.None));
                }
            }
            else
            {
                segments.Add(segment);
            }

            column += segmentWidth;
        }

        return new StyledLine(segments, line.IsSelected);
    }

    private static void Append(List<StyledSegment> cells, StyledSegment cell)
    {
        if (cells.Count > 0 && cells[^1].Role == cell.Role && cells[^1].ColorIndex == cell.ColorIndex && cells[^1].Selection == cell.Selection)
        {
            cells[^1] = cells[^1] with { Text = cells[^1].Text + cell.Text };
        }
        else
        {
            cells.Add(cell);
        }
    }

    private char Glyph(LineDirections directions)
    {
        if (!_glyphs.IsAscii)
        {
            return _glyphs.For(directions);
        }

        return directions switch
        {
            LineDirections.Up or LineDirections.Down or (LineDirections.Up | LineDirections.Down) => '|',
            LineDirections.Left or LineDirections.Right or (LineDirections.Left | LineDirections.Right) => '-',
            _ => '+',
        };
    }

    // The box is drawn one cell outside the content rectangle; the top edge row is the title row of the detail and diff panes.
    private void AddBox(PaneRect content, bool isFocused, string title)
    {
        if (content.IsEmpty)
        {
            return;
        }

        var left = content.X - 1;
        var right = content.X + content.Width;
        var top = content.Y - 1;
        var bottom = content.Y + content.Height;
        Horizontal(top, left, right, isFocused);
        Horizontal(bottom, left, right, isFocused);
        Vertical(left, top, bottom, isFocused);
        Vertical(right, top, bottom, isFocused);
        PlaceTitle(title, left, top, content.Width, isFocused);
    }

    private void Horizontal(int row, int from, int to, bool isFocused)
    {
        for (var column = from; column <= to; column++)
        {
            var toLeft = column > from ? LineDirections.Left : LineDirections.None;
            var toRight = column < to ? LineDirections.Right : LineDirections.None;
            Connect(column, row, toLeft | toRight, isFocused);
        }
    }

    private void Vertical(int column, int from, int to, bool isFocused)
    {
        for (var row = from; row <= to; row++)
        {
            var toUp = row > from ? LineDirections.Up : LineDirections.None;
            var toDown = row < to ? LineDirections.Down : LineDirections.None;
            Connect(column, row, toUp | toDown, isFocused);
        }
    }

    private void Connect(int column, int row, LineDirections directions, bool isFocused)
    {
        if (column < 0 || column >= _width || row < 0 || row >= _height)
        {
            return;
        }

        var index = row * _width + column;
        _connections[index] |= directions;
        _focusedCells[index] |= isFocused;
    }

    // " Title " as a chip after one stretch of line, when there is room for it and a stretch of line after it.
    // A long title is cut with an ellipsis; a wide character takes its first column and leaves the next one empty.
    private void PlaceTitle(string title, int left, int row, int innerWidth, bool isFocused)
    {
        var fitted = TextWidth.Fit(title, innerWidth - 4, _glyphs.Ellipsis);
        if (fitted.Length == 0)
        {
            return;
        }

        var role = isFocused ? SegmentRole.ActiveTab : SegmentRole.SelectedTab;
        var column = left + 2;
        var counter = new WidthCounter();
        var textIndex = -1;
        foreach (var rune in ($" {fitted} ").EnumerateRunes())
        {
            var runeWidth = counter.Next(rune);
            if (runeWidth == 0 && textIndex >= 0)
            {
                // A joined or combining character shares the cell of the character it follows.
                if (_titleCells.TryGetValue(textIndex, out var previous))
                {
                    _titleCells[textIndex] = previous with { Text = $"{previous.Text}{rune}" };
                }

                continue;
            }

            textIndex = row * _width + column;
            for (var offset = 0; offset < Math.Max(1, runeWidth); offset++)
            {
                if (column + offset >= 0 && column + offset < _width && row >= 0 && row < _height)
                {
                    _titleCells[row * _width + column + offset] = new StyledSegment(offset == 0 ? rune.ToString() : string.Empty, role, Selection: SegmentSelection.None);
                }
            }

            column += Math.Max(1, runeWidth);
        }
    }
}
