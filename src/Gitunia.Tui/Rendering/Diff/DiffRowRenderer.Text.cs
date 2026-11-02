using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.Rendering.Diff;

// Shown text: tab expansion, whitespace marks and cell flags.
public static partial class DiffRowRenderer
{
    /// <summary>Index in the whole text of the first character of chunk <paramref name="segment"/>.</summary>
    internal static int StartOf(List<string> chunks, int segment)
    {
        var start = 0;
        for (var index = 0; index < segment && index < chunks.Count; index++)
        {
            start += chunks[index].Length;
        }

        return start;
    }

    /// <summary>
    /// <see cref="ExpandTabs"/> that can also mark whitespace: a tab becomes the tab mark and spaces up to the stop,
    /// a space the space mark (a trailing one a heavier one), a carriage return the CR mark. Every mark takes the one
    /// column the character it replaces took, so wrapping and cutting are the same with or without marks. The
    /// characters of the <paramref name="changes"/> stretches are flagged as changed.
    /// </summary>
    /// <param name="text">The row text.</param>
    /// <param name="marks">Supplies the marks; null draws none.</param>
    /// <param name="isCut"><paramref name="text"/> is only the start of a line, so it has no trailing spaces.</param>
    /// <param name="changes">Stretches of <paramref name="text"/> that differ from the paired line, or null.</param>
    internal static ShownText Expand(
        string text,
        GraphGlyphSet? marks,
        bool isCut,
        IReadOnlyList<ChangedSpan>? changes = null)
    {
        var hasChanges = changes is { Count: > 0 };
        var trailingFrom = isCut ? text.Length : TrailingSpacesFrom(text);
        var needsMarks = marks is not null && (text.Contains(' ') || ContainsControl(text));
        if (!hasChanges && !needsMarks)
        {
            return new ShownText(ExpandTabs(text), null);
        }

        var builder = new StringBuilder(text.Length + 8);
        var cells = new List<CellFlags>(text.Length + 8);
        var column = 0;
        var index = 0;
        var spanIndex = 0;
        var counter = new WidthCounter();
        foreach (var rune in text.EnumerateRunes())
        {
            while (hasChanges && spanIndex < changes!.Count && changes[spanIndex].End <= index)
            {
                spanIndex++;
            }

            var isChanged = hasChanges && spanIndex < changes!.Count && index >= changes[spanIndex].Start;
            var start = builder.Length;
            var isMark = false;
            if (rune.Value == '\t')
            {
                var spaces = TabWidth - (column % TabWidth);
                isMark = marks is not null;
                builder.Append(marks is null ? ' ' : marks.TabMark).Append(' ', spaces - 1);
                column += spaces;
            }
            else if (rune.Value == '\r' && marks is not null)
            {
                isMark = true;
                builder.Append(marks.CarriageReturnMark);
                column++;
            }
            else if (Rune.IsControl(rune))
            {
                builder.Append(' ');
                column++;
            }
            else if (rune.Value == ' ' && marks is not null)
            {
                isMark = true;
                builder.Append(index >= trailingFrom ? marks.SpaceMark : marks.InnerSpaceMark);
                column++;
            }
            else
            {
                builder.Append(rune.ToString());
                column += counter.Next(rune);
            }

            AddCells(cells, builder.Length - start, isMark, isChanged);
            index += rune.Utf16SequenceLength;
        }

        return new ShownText(builder.ToString(), [.. cells]);
    }

    // The flags of the characters one source character was shown as: only the first of a tab's columns is the mark.
    private static void AddCells(List<CellFlags> cells, int count, bool isMark, bool isChanged)
    {
        for (var position = 0; position < count; position++)
        {
            var flags = isChanged ? CellFlags.Changed : CellFlags.None;
            cells.Add(isMark && position == 0 ? flags | CellFlags.Mark : flags);
        }
    }

    // The index where the run of spaces at the end of the text starts, ignoring a carriage return after it (CRLF);
    // the length when the text does not end in a space.
    private static int TrailingSpacesFrom(string text)
    {
        var index = text.Length;
        if (index > 0 && text[index - 1] == '\r')
        {
            index--;
        }

        while (index > 0 && text[index - 1] == ' ')
        {
            index--;
        }

        return index;
    }

    /// <summary>Text as drawn and the flags of each of its characters (null when none is a mark or changed).</summary>
    internal readonly record struct ShownText(string Text, CellFlags[]? Cells);

    /// <summary>What a character of the shown text is besides its row's colour.</summary>
    [Flags]
    internal enum CellFlags : byte
    {
        None = 0,

        /// <summary>A whitespace mark (tab, space or carriage return glyph).</summary>
        Mark = 1,

        /// <summary>Part of a stretch that differs from the paired line.</summary>
        Changed = 2,
    }


    /// <summary>The text as shown: tabs become spaces up to the next tab stop, other control characters become one space.</summary>
    public static string ExpandTabs(string text)
    {
        if (!ContainsControl(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        var column = 0;
        var counter = new WidthCounter();
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\t')
            {
                var spaces = TabWidth - (column % TabWidth);
                builder.Append(' ', spaces);
                column += spaces;
            }
            else if (Rune.IsControl(rune))
            {
                builder.Append(' ');
                column++;
            }
            else
            {
                builder.Append(rune.ToString());
                column += counter.Next(rune);
            }
        }

        return builder.ToString();
    }

    private static bool ContainsControl(string text)
    {
        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
