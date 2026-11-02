namespace Gitunia.Core.Conflicts;

public sealed partial record ConflictEditorState
{
    /// <summary>
    /// Scrolls the panes so the cursor shows: the active pane keeps its window while the cursor is inside it, the
    /// others keep their window while the aligned line shows in it and else scroll to it at the same height.
    /// Idempotent for the same heights.
    /// </summary>
    /// <param name="versionRows">Rows of the base, ours and theirs panes.</param>
    /// <param name="resultRows">Rows of the result pane.</param>
    /// <param name="isCentered">
    /// Put the cursor a third down the panes instead of scrolling the least (after a jump to a conflict, so the lines
    /// after it show too).
    /// </param>
    public ConflictEditorState Follow(int versionRows, int resultRows, bool isCentered = false)
    {
        versionRows = Math.Max(1, versionRows);
        resultRows = Math.Max(1, resultRows);
        int top;
        int result;
        if (isCentered)
        {
            var line = ActivePane == ConflictPane.Result ? Result.Editor.CursorRow : ResultLineOfRow(Row);
            top = Row - (versionRows / CenterDivisor);
            result = line - (resultRows / CenterDivisor);
        }
        else if (ActivePane == ConflictPane.Result)
        {
            var line = Result.Editor.CursorRow;
            result = ScrollTop(line, ResultScroll, resultRows);
            top = AlignedScroll(Row, TopScroll, versionRows, line - result);
        }
        else
        {
            top = ScrollTop(Row, TopScroll, versionRows);
            result = AlignedScroll(ResultLineOfRow(Row), ResultScroll, resultRows, Row - top);
        }

        top = Math.Clamp(top, 0, Math.Max(0, Alignment.RowCount - versionRows));
        result = Math.Clamp(result, 0, Math.Max(0, ResultLineCount - resultRows));

        return top == TopScroll && result == ResultScroll ? this : this with { TopScroll = top, ResultScroll = result };
    }

    // The other pane keeps its window while the aligned line shows in it; else it scrolls to put that line at the same
    // height as the cursor has in the active pane.
    private static int AlignedScroll(int line, int top, int height, int offset)
    {
        if (line >= top && line < top + height)
        {
            return top;
        }

        return line - Math.Min(offset, height - 1);
    }

    private static int ScrollTop(int cursor, int top, int height)
    {
        if (cursor < top)
        {
            return cursor;
        }

        return cursor >= top + height ? cursor - height + 1 : Math.Max(0, top);
    }
}
