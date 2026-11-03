using Gitunia.Core.Diff;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// Where the text of the diff is on screen, for character-wise selection: which document row a screen line (a list item) shows
/// on a side, which columns of the line its text takes, and which part of the row's shown text (tabs expanded) the line holds
/// once the row wraps. Positions of a selection are a screen line and the UTF-16 index of a character in the text of the row
/// that line shows; <see cref="ToRows"/> turns them into rows for the copy.
/// </summary>
/// <param name="document">The diff.</param>
/// <param name="wrap">The wrapped layout of the unified view, null when rows are not wrapped or the view is side by side.</param>
/// <param name="split">The side-by-side layout, null in the unified view.</param>
/// <param name="width">Columns of the text area of the pane.</param>
/// <param name="gutterDigits">Digits of a line number column.</param>
public sealed class DiffTextGeometry(
    DiffDocument document,
    DiffWrapMap? wrap,
    DiffSplitMap? split,
    int width,
    int gutterDigits)
{
    /// <summary>The side-by-side view is shown.</summary>
    public bool IsSplit => split is not null;

    /// <summary>Screen lines in all.</summary>
    public int Count => split?.Count ?? wrap?.Count ?? document.Rows.Count;

    /// <summary>The document row shown on screen line <paramref name="item"/> (side by side: the one on <paramref name="isOldSide"/>); null when that side is empty there.</summary>
    /// <param name="item">The screen line.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    public int? RowOf(int item, bool isOldSide)
    {
        if (item < 0 || item >= Count)
        {
            return null;
        }

        if (split is not null)
        {
            var line = split.Split.Lines[split.SplitLineOf(item)];
            var row = line.IsFull || isOldSide ? line.LeftRow : line.RightRow;

            return row < 0 ? null : row;
        }

        return wrap?.RowOf(item) ?? item;
    }

    /// <summary>Where the text of the row on screen line <paramref name="item"/> is, null when there is none (outside the lines, an empty side).</summary>
    /// <param name="item">The screen line.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    public DiffTextCell? CellOf(int item, bool isOldSide)
    {
        if (item < 0 || item >= Count)
        {
            return null;
        }

        if (split is not null)
        {
            return SplitCell(item, isOldSide);
        }

        if (wrap is not null)
        {
            var row = wrap.RowOf(item);

            return Cell(row, item - wrap.StartOf(row), width, 0, PrefixOf(row), true);
        }

        return Cell(item, 0, width, 0, PrefixOf(item), false);
    }

    /// <summary>Whether the pointer at <paramref name="x"/> (a column of the text area) is on the old (left) side of the side-by-side view.</summary>
    /// <param name="x">The column.</param>
    public bool IsOldSideAt(int x) => split is not null && x < DiffSplitRenderer.LeftWidth(width);

    /// <summary>The character the pointer at <paramref name="x"/> is over on screen line <paramref name="item"/>.</summary>
    /// <param name="item">The screen line.</param>
    /// <param name="x">The column inside the text area of the pane.</param>
    /// <param name="isOldSide">Stay on this side of the side-by-side view; null takes the side the pointer is on.</param>
    /// <returns>The position and its side; the start of the line when the row has no text on the side.</returns>
    public TextHit PositionAt(int item, int x, bool? isOldSide)
    {
        var side = isOldSide ?? IsOldSideAt(x);
        if (CellOf(item, side) is not { } cell)
        {
            return new TextHit(new TextPosition(item, 0), side);
        }

        var text = TextOf(cell.Row);
        var column = Math.Max(x - cell.LineStart - cell.Prefix, 0) + cell.First;
        column = Math.Min(column, Math.Max(cell.End - 1, cell.First));

        return new TextHit(new TextPosition(item, ShownColumns.IndexAt(text, column, DiffRowRenderer.TabWidth)), side);
    }

    /// <summary>The position at the start of what screen line <paramref name="item"/> shows of its row.</summary>
    /// <param name="item">The screen line.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    public TextPosition StartOf(int item, bool isOldSide)
    {
        if (CellOf(item, isOldSide) is not { } cell || cell.First == 0)
        {
            return new TextPosition(item, 0);
        }

        return new TextPosition(item, ShownColumns.IndexAt(TextOf(cell.Row), cell.First, DiffRowRenderer.TabWidth));
    }

    /// <summary>The screen line that shows the character at <paramref name="position"/> (the line of its row that holds its column); the line itself when the row has no text there.</summary>
    /// <param name="position">A screen line of the row and a character index in the row's text.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    public int ItemShowing(TextPosition position, bool isOldSide)
    {
        if (CellOf(position.Line, isOldSide) is not { } cell)
        {
            return position.Line;
        }

        var text = TextOf(cell.Row);
        var shown = ShownColumns.OfIndex(text, Math.Min(position.Column, text.Length), DiffRowRenderer.TabWidth);
        var first = position.Line - cell.Segment;
        for (var segment = cell.SegmentCount - 1; segment > 0; segment--)
        {
            if (CellOf(first + segment, isOldSide) is { } later && shown >= later.First)
            {
                return first + segment;
            }
        }

        return first;
    }

    /// <summary>The columns of the text area that the characters of <paramref name="span"/> take on screen line <paramref name="item"/>; null when none of them are on it.</summary>
    /// <param name="item">The screen line.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    /// <param name="span">The characters of the row the line shows.</param>
    public DiffTextColumns? ColumnsOf(int item, bool isOldSide, ColumnSpan span)
    {
        if (CellOf(item, isOldSide) is not { } cell)
        {
            return null;
        }

        var text = TextOf(cell.Row);
        var from = ShownColumns.OfIndex(text, span.First, DiffRowRenderer.TabWidth);
        var to = ShownColumns.OfIndex(text, span.Last, DiffRowRenderer.TabWidth)
            + ShownColumns.WidthAt(text, span.Last, DiffRowRenderer.TabWidth);
        from = Math.Max(from, cell.First);
        to = Math.Min(to, cell.End);
        if (to <= from)
        {
            return null;
        }

        var origin = cell.LineStart + cell.Prefix - cell.First;

        return new DiffTextColumns(origin + from, origin + to);
    }

    /// <summary>
    /// The selection in rows: its ends are the rows of the screen lines they sit on (side by side, on <paramref name="isOldSide"/>),
    /// so a row that wraps over several lines counts once. An end on a line that has no row on the side moves to the next row
    /// that has one (the start) or the previous one (the end).
    /// </summary>
    /// <param name="selection">The selection in screen lines.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    /// <returns>The selection, null when it covers no row of the side.</returns>
    public TextSelection? ToRows(TextSelection selection, bool isOldSide)
    {
        var anchorFirst = !selection.Anchor.IsAfter(selection.Cursor);
        if (RowEnd(selection.Anchor, isOldSide, anchorFirst) is not { } anchor
            || RowEnd(selection.Cursor, isOldSide, !anchorFirst) is not { } cursor)
        {
            return null;
        }

        // The earlier end moved forward and the later one back: if they crossed, no row of the side is covered.
        var first = anchorFirst ? anchor : cursor;
        var last = anchorFirst ? cursor : anchor;

        return first.Line > last.Line ? null : new TextSelection(anchor, cursor);
    }

    /// <summary>Whether row <paramref name="row"/> is part of the text of a side (a removed row is not on the new side, an added one not on the old side).</summary>
    /// <param name="row">The row.</param>
    /// <param name="isOldSide">The left side of the side-by-side view.</param>
    public bool IsOnSide(int row, bool isOldSide) => !IsSplit || document.Rows[row].Kind switch
    {
        DiffRowKind.Removed => isOldSide,
        DiffRowKind.Added => !isOldSide,
        _ => true,
    };

    /// <summary>The text of row <paramref name="row"/> as it is copied.</summary>
    /// <param name="row">The row.</param>
    public string TextOf(int row) => DiffTextWriter.Line(document.Rows[row]);

    private TextPosition? RowEnd(TextPosition position, bool isOldSide, bool isStart)
    {
        if (RowOf(position.Line, isOldSide) is { } row)
        {
            return new TextPosition(row, position.Column);
        }

        var step = isStart ? 1 : -1;
        for (var item = position.Line + step; item >= 0 && item < Count; item += step)
        {
            if (RowOf(item, isOldSide) is { } found)
            {
                return new TextPosition(found, isStart ? 0 : int.MaxValue);
            }
        }

        return null;
    }

    private DiffTextCell? SplitCell(int item, bool isOldSide)
    {
        var map = split!;
        var index = map.SplitLineOf(item);
        var segment = item - map.StartOfSplitLine(index);
        var line = map.Split.Lines[index];
        if (line.IsFull)
        {
            return Cell(line.LeftRow, segment, width, 0, PrefixOf(line.LeftRow), map.IsWrapped);
        }

        var row = isOldSide ? line.LeftRow : line.RightRow;
        if (row < 0)
        {
            return null;
        }

        var sideWidth = isOldSide ? DiffSplitRenderer.LeftWidth(width) : DiffSplitRenderer.RightWidth(width);
        var start = isOldSide ? 0 : DiffSplitRenderer.LeftWidth(width) + 1;

        return Cell(row, segment, sideWidth, start, DiffSplitRenderer.SidePrefixWidth(gutterDigits), map.IsWrapped);
    }

    private int PrefixOf(int row) => DiffRowRenderer.PrefixWidth(document.Rows[row], gutterDigits);

    private DiffTextCell? Cell(int row, int segment, int sideWidth, int lineStart, int prefix, bool isWrapped)
    {
        var columns = sideWidth - prefix;
        if (columns <= 0 && (isWrapped || split is not null))
        {
            return null;
        }

        if (!isWrapped)
        {
            // Side by side the text is cut to its half; in the unified view the line is as wide as the pane.
            var end = split is null || lineStart == 0 && sideWidth == width ? int.MaxValue : columns;

            return new DiffTextCell(row, lineStart, prefix, 0, end, 0, 1);
        }

        var chunks = DiffRowRenderer.Chunks(DiffRowRenderer.ExpandTabs(document.Rows[row].Text), columns);
        if (segment >= chunks.Count)
        {
            return null;
        }

        var first = 0;
        for (var index = 0; index < segment; index++)
        {
            first += TextWidth.Of(chunks[index]);
        }

        return new DiffTextCell(row, lineStart, prefix, first, first + TextWidth.Of(chunks[segment]), segment, chunks.Count);
    }
}
