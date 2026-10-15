using System.Globalization;
using Gitunia.Core.Editing;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>
/// Draws a <see cref="MessageEditor"/> into styled lines of an exact width: the cursor is a <see cref="SegmentRole.Cursor"/>
/// cell, the part of the subject past <see cref="MessageEditor.SubjectSoftLimit"/> columns is an <see cref="SegmentRole.Error"/>
/// hint (never enforced). Long lines are cut at the right edge; the cursor's own line scrolls sideways to keep it visible.
/// </summary>
public static class MessageEditorRenderer
{
    private const string Blank = " ";

    /// <summary>The visible rows of the editor for a box of <paramref name="width"/> by <paramref name="height"/> cells.</summary>
    public static IReadOnlyList<StyledLine> Render(MessageEditor editor, int width, int height, bool showCursor)
    {
        var lines = new List<StyledLine>(height);
        var top = Math.Max(0, editor.CursorRow - height + 1);
        for (var row = top; row < top + height; row++)
        {
            var text = row < editor.Lines.Count ? editor.Lines[row] : string.Empty;
            var cursorColumn = showCursor && row == editor.CursorRow ? editor.CursorColumn : -1;
            lines.Add(RenderLine(text, width, cursorColumn, row == 0));
        }

        return lines;
    }

    private static StyledLine RenderLine(string text, int width, int cursorColumn, bool isSubject)
    {
        var elements = SplitElements(text);
        var segments = new List<StyledSegment>();
        var used = 0;
        var offset = 0;
        var first = cursorColumn < 0 ? 0 : FirstVisibleElement(elements, cursorColumn, width);
        var skipped = WidthOf(elements, first);
        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            var isCursor = offset == cursorColumn;
            offset += element.Length;
            if (index < first)
            {
                continue;
            }

            var elementWidth = TextWidth.Of(element);
            if (used + elementWidth > width)
            {
                break;
            }

            var isOverlong = isSubject && skipped + used >= MessageEditor.SubjectSoftLimit;
            var role = isCursor ? SegmentRole.Cursor : isOverlong ? SegmentRole.Error : SegmentRole.Plain;
            Append(segments, Displayable(element), role);
            used += elementWidth;
        }

        if (offset == cursorColumn && cursorColumn == text.Length && used < width)
        {
            Append(segments, Blank, SegmentRole.Cursor);
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
    private static string Displayable(string element) => element.Length == 1 && char.IsControl(element[0]) ? Blank : element;

    private static void Append(List<StyledSegment> segments, string text, SegmentRole role)
    {
        if (segments.Count > 0 && segments[^1].Role == role && role is SegmentRole.Plain or SegmentRole.Error)
        {
            segments[^1] = segments[^1] with { Text = $"{segments[^1].Text}{text}" };
            return;
        }

        segments.Add(new StyledSegment(text, role));
    }
}
