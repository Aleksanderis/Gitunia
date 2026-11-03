using System.Globalization;
using Gitunia.Core.Editing;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>
/// Draws a <see cref="MessageEditor"/> into styled lines of an exact width: the cursor is a
/// <see cref="SegmentRole.Cursor"/> cell, the part of the subject past <see cref="MessageEditor.SubjectSoftLimit"/>
/// columns is an <see cref="SegmentRole.Error"/> hint (never enforced). Long lines are cut at the right edge; the
/// cursor's own line scrolls sideways to keep it visible. Vertical scrolling is stateless: the cursor row is the last
/// visible row once the text is taller than the box.
/// </summary>
public static class MessageEditorRenderer
{
    private const string Blank = " ";

    /// <summary>
    /// The first visible row once the cursor row must show: <paramref name="top"/> is kept while the cursor is inside the window, so
    /// moving up inside a long message does not shift the text.
    /// </summary>
    /// <param name="cursorRow">The cursor's line.</param>
    /// <param name="top">The first visible row so far.</param>
    /// <param name="height">Visible rows.</param>
    public static int ScrollTop(int cursorRow, int top, int height)
    {
        if (height <= 0)
        {
            return 0;
        }

        if (cursorRow < top)
        {
            return cursorRow;
        }

        return cursorRow >= top + height ? cursorRow - height + 1 : Math.Max(0, top);
    }

    /// <summary>
    /// Display columns the cursor's line is scrolled sideways while it is drawn with the cursor in a box of
    /// <paramref name="width"/> columns; the other lines are not scrolled. A click at cell <c>c</c> of that line is column
    /// <c>c</c> plus this.
    /// </summary>
    /// <param name="editor">The message.</param>
    /// <param name="width">Text columns of the box.</param>
    public static int ScrollOffset(MessageEditor editor, int width)
    {
        var elements = SplitElements(editor.Lines[editor.CursorRow]);

        return WidthOf(elements, FirstVisibleElement(elements, editor.CursorColumn, width));
    }

    /// <summary>The visible rows for a box of <paramref name="width"/> by <paramref name="height"/> cells.</summary>
    /// <param name="editor">The message.</param>
    /// <param name="width">Columns.</param>
    /// <param name="height">Rows.</param>
    /// <param name="showCursor">Draw the cursor.</param>
    /// <param name="marksSubject">Mark the first line past the soft limit (a commit subject, not code).</param>
    /// <param name="scrollTop">The first visible row kept by the caller (see <see cref="ScrollTop"/>); null scrolls statelessly: the cursor row is the last visible row once the text is taller than the box.</param>
    public static IReadOnlyList<StyledLine> Render(
        MessageEditor editor,
        int width,
        int height,
        bool showCursor,
        int? scrollTop = null,
        bool marksSubject = true)
    {
        height = Math.Max(0, height);
        width = Math.Max(0, width);
        var lines = new List<StyledLine>(height);
        var top = scrollTop ?? Math.Max(0, editor.CursorRow - height + 1);
        var selection = editor.Selection;
        for (var row = top; row < top + height; row++)
        {
            var text = row < editor.Lines.Count ? editor.Lines[row] : string.Empty;
            var cursorColumn = showCursor && row == editor.CursorRow ? editor.CursorColumn : -1;
            var selected = row < editor.Lines.Count ? selection?.InLine(row, text.Length) : null;
            var includesBreak = selection?.IncludesLineBreak(row) == true;
            lines.Add(RenderLine(text, width, cursorColumn, marksSubject && row == 0, selected, includesBreak));
        }

        return lines;
    }

    private static StyledLine RenderLine(
        string text,
        int width,
        int cursorColumn,
        bool isSubject,
        ColumnRange? selected,
        bool selectsLineBreak)
    {
        var elements = SplitElements(text);
        var segments = new List<StyledSegment>();
        var used = 0;
        var offset = 0;
        var first = cursorColumn < 0 ? 0 : FirstVisibleElement(elements, cursorColumn, width);
        var skipped = WidthOf(elements, first);
        var isCut = false;
        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            var isCursor = offset == cursorColumn;
            var offsetBefore = offset;
            offset += element.Length;
            if (index < first)
            {
                continue;
            }

            var elementWidth = TextWidth.Of(element);
            if (used + elementWidth > width)
            {
                isCut = true;
                break;
            }

            var isSelected = selected is { } range && offsetBefore >= range.Start && offsetBefore < range.End;
            var isOverlong = isSubject && skipped + used >= MessageEditor.SubjectSoftLimit;
            var role = SegmentRole.Plain;
            if (isCursor)
            {
                role = SegmentRole.Cursor;
            }
            else if (isOverlong)
            {
                role = SegmentRole.Error;
            }

            Append(segments, Displayable(element), role, isSelected);
            used += elementWidth;
        }

        if (offset == cursorColumn && cursorColumn == text.Length && used < width)
        {
            Append(segments, Blank, SegmentRole.Cursor);
            used++;
        }
        else if (selectsLineBreak && !isCut && used < width)
        {
            // The selection runs on to the next line: the line break shows as one selected cell.
            Append(segments, Blank, SegmentRole.Plain, true);
            used++;
        }

        if (used < width)
        {
            Append(segments, new string(' ', width - used), SegmentRole.Plain);
        }

        return new StyledLine(segments);
    }

    // The first element to draw: leading ones are dropped until the cursor's element fits in the width.
    private static int FirstVisibleElement(List<string> elements, int cursorColumn, int width)
    {
        var offset = 0;
        var cursorIndex = elements.Count;
        for (var index = 0; index < elements.Count; index++)
        {
            if (offset == cursorColumn)
            {
                cursorIndex = index;
                break;
            }

            offset += elements[index].Length;
        }

        var cursorWidth = cursorIndex < elements.Count ? TextWidth.Of(elements[cursorIndex]) : 1;
        var first = 0;
        var before = WidthOf(elements, cursorIndex);
        while (first < cursorIndex && before + cursorWidth > width)
        {
            before -= TextWidth.Of(elements[first]);
            first++;
        }

        return first;
    }

    private static List<string> SplitElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    private static int WidthOf(List<string> elements, int count)
    {
        var width = 0;
        for (var index = 0; index < count; index++)
        {
            width += TextWidth.Of(elements[index]);
        }

        return width;
    }

    // Control characters (a tab) are shown as a space.
    private static string Displayable(string element) =>
        element.Length == 1 && char.IsControl(element[0]) ? Blank : element;

    private static void Append(List<StyledSegment> segments, string text, SegmentRole role, bool isSelected = false)
    {
        var selection = isSelected ? SegmentSelection.Range : SegmentSelection.Inherit;
        if (segments.Count > 0
            && segments[^1].Role == role
            && segments[^1].Selection == selection
            && role is SegmentRole.Plain or SegmentRole.Error)
        {
            segments[^1] = segments[^1] with { Text = $"{segments[^1].Text}{text}" };
            return;
        }

        segments.Add(new StyledSegment(text, role, 0, selection));
    }
}
