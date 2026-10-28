using System.Globalization;
using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>Draws one row of the diff pane: line numbers, change marker and text, cut or wrapped.</summary>
public static class DiffRowRenderer
{
    /// <summary>Width of a tab stop; tabs in the diff are shown as spaces up to the next stop.</summary>
    public const int TabWidth = 4;

    private const int MinimumGutterDigits = 3;
    private const int CutMargin = 16;
    private const int HunkRuleWidth = 2;

    /// <summary>Digits each line number column needs for a document whose largest line number is <paramref name="maximumLineNumber"/>.</summary>
    public static int GutterDigits(int maximumLineNumber) =>
        Math.Max(MinimumGutterDigits, maximumLineNumber.ToString(CultureInfo.InvariantCulture).Length);

    /// <param name="row">The row.</param>
    /// <param name="width">Columns of the pane.</param>
    /// <param name="gutterDigits">Digits of each line number column, from <see cref="GutterDigits"/>.</param>
    /// <param name="highlight">Text to highlight (the diff search), null for none.</param>
    /// <param name="isSelected">The row is under the cursor.</param>
    /// <param name="glyphs">Supplies the ellipsis.</param>
    /// <param name="jump">The quick-jump label of the row, null for none.</param>
    /// <param name="showWhitespace">Draw tabs, spaces and carriage returns as marks (same width).</param>
    /// <param name="changes">The stretches of the text that differ from the paired line, null for none.</param>
    public static StyledLine Render(
        DiffRow row,
        int width,
        int gutterDigits,
        string? highlight,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump = null,
        bool showWhitespace = false,
        IReadOnlyList<ChangedSpan>? changes = null)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        switch (row.Kind)
        {
            case DiffRowKind.FileHeader:
                AppendText(builder, row.Text, SegmentRole.DiffFile, highlight, width, jump: jump);
                break;
            case DiffRowKind.HunkHeader:
                return RenderHunk(row, 0, width, highlight, isSelected, glyphs, jump, false);
            case DiffRowKind.Warning:
                AppendText(builder, row.Text, SegmentRole.Error, highlight, width, "! ", jump);
                break;
            case DiffRowKind.Note:
            case DiffRowKind.NoNewline:
                AppendText(builder, row.Text, SegmentRole.Faint, highlight, width, "  ", jump);
                break;
            default:
                AppendChange(
                    builder, row, width, gutterDigits, highlight, jump, showWhitespace ? glyphs : null, changes);
                break;
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>Columns taken before the text of <paramref name="row"/> (line numbers and marker, or the note prefix).</summary>
    public static int PrefixWidth(DiffRow row, int gutterDigits) => row.Kind switch
    {
        DiffRowKind.FileHeader => 0,
        DiffRowKind.HunkHeader => HunkRuleWidth + 1,
        DiffRowKind.Warning or DiffRowKind.Note or DiffRowKind.NoNewline => 2,
        _ when IsContent(row) => gutterDigits + 2,
        _ => (2 * gutterDigits) + 3,
    };

    /// <summary>A file content line: one number column, no marker.</summary>
    private static bool IsContent(DiffRow row) => row.Kind == DiffRowKind.Content;

    private static string Gutter(DiffRow row, int gutterDigits) => IsContent(row)
        ? $"{Number(row.NewLineNumber, gutterDigits)}  "
        : $"{Number(row.OldLineNumber, gutterDigits)} {Number(row.NewLineNumber, gutterDigits)} ";

    /// <summary>How many screen lines <paramref name="row"/> takes when its text wraps at <paramref name="width"/> columns.</summary>
    public static int SegmentCount(DiffRow row, int width, int gutterDigits)
    {
        return TextSegmentCount(row.Text, width - PrefixWidth(row, gutterDigits));
    }

    /// <summary>How many screen lines <paramref name="text"/> takes when it wraps at <paramref name="columns"/> columns.</summary>
    public static int TextSegmentCount(string text, int columns)
    {
        var fitsOneLine = text.Length * 2 <= columns && !ContainsControl(text);
        return columns <= 0 || fitsOneLine ? 1 : Chunks(ExpandTabs(text), columns).Count;
    }

    /// <summary>Cuts <paramref name="shown"/> into pieces of at most <paramref name="columns"/> columns; a wide character is never split.</summary>
    public static List<string> Chunks(string shown, int columns)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        var used = 0;
        var counter = new WidthCounter();
        foreach (var rune in shown.EnumerateRunes())
        {
            var runeWidth = counter.Next(rune);
            if (used > 0 && used + runeWidth > columns)
            {
                chunks.Add(builder.ToString());
                builder.Clear();
                used = 0;
            }

            builder.Append(rune.ToString());
            used += runeWidth;
        }

        chunks.Add(builder.ToString());
        return chunks;
    }

    /// <summary>One screen line of a wrapped row: segment 0 carries the line numbers and marker, the others a blank prefix.</summary>
    public static StyledLine RenderSegment(
        DiffRow row,
        int segment,
        int width,
        int gutterDigits,
        string? highlight,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump = null,
        bool showWhitespace = false,
        IReadOnlyList<ChangedSpan>? changes = null)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        var prefixWidth = PrefixWidth(row, gutterDigits);
        var columns = width - prefixWidth;
        var textRole = row.Kind switch
        {
            DiffRowKind.FileHeader => SegmentRole.DiffFile,
            DiffRowKind.HunkHeader => SegmentRole.DiffHunk,
            DiffRowKind.Warning => SegmentRole.Error,
            DiffRowKind.Note or DiffRowKind.NoNewline => SegmentRole.Faint,
            DiffRowKind.Added => SegmentRole.DiffAdded,
            DiffRowKind.Removed => SegmentRole.DiffRemoved,
            _ => SegmentRole.Plain,
        };
        if (columns <= 0)
        {
            return Render(row, width, gutterDigits, highlight, isSelected, glyphs, jump, showWhitespace, changes);
        }

        if (row.Kind == DiffRowKind.HunkHeader)
        {
            return RenderHunk(row, segment, width, highlight, isSelected, glyphs, jump, true);
        }

        if (segment == 0)
        {
            switch (row.Kind)
            {
                case DiffRowKind.Warning:
                    builder.Append("! ", textRole);
                    break;
                case DiffRowKind.Note or DiffRowKind.NoNewline:
                    builder.Append("  ", textRole);
                    break;
                case DiffRowKind.Context or DiffRowKind.Content or DiffRowKind.Added or DiffRowKind.Removed:
                    builder.Append(Gutter(row, gutterDigits), SegmentRole.Faint);
                    if (!IsContent(row))
                    {
                        var marker = row.Kind == DiffRowKind.Added ? "+" : row.Kind == DiffRowKind.Removed ? "-" : " ";
                        builder.Append(marker, textRole == SegmentRole.Plain ? SegmentRole.Faint : textRole);
                    }

                    break;
            }
        }
        else
        {
            builder.Append(new string(' ', prefixWidth), SegmentRole.Plain);
        }

        var isChange = row.Kind is DiffRowKind.Context
            or DiffRowKind.Content
            or DiffRowKind.Added
            or DiffRowKind.Removed;
        var expanded = Expand(row.Text, showWhitespace && isChange ? glyphs : null, false, changes);
        var chunks = Chunks(expanded.Text, columns);
        var shown = segment < chunks.Count ? chunks[segment] : string.Empty;
        if (segment == 0 && jump is { } target)
        {
            JumpOverlay.Append(builder, shown, textRole, target);
        }
        else
        {
            AppendHighlighted(builder, shown, textRole, highlight, expanded.Cells, StartOf(chunks, segment));
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

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
    /// <param name="changes">Stretches of <paramref name="text"/> that differ from the paired line, null for none.</param>
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

    /// <summary>
    /// A hunk header as a separator: a short rule, the header text and a rule to the right edge (the glyph is the
    /// frame's horizontal line, so it shows without colour and in the ASCII set). With <paramref name="wrap"/> the text
    /// continues on following screen lines and the rule closes the last one.
    /// </summary>
    private static StyledLine RenderHunk(
        DiffRow row,
        int segment,
        int width,
        string? highlight,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump,
        bool wrap)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        var rule = glyphs.Horizontal.ToString();
        var columns = width - HunkRuleWidth - 1;
        var chunks = wrap && columns > 0
            ? Chunks(ExpandTabs(row.Text), columns)
            : [ExpandTabs(CutToWidth(row.Text, width))];
        var shown = segment < chunks.Count ? chunks[segment] : string.Empty;
        builder.Append(segment == 0 ? $"{rule}{rule} " : new string(' ', HunkRuleWidth + 1), SegmentRole.Faint);
        if (segment == 0 && jump is { } target)
        {
            JumpOverlay.Append(builder, shown, SegmentRole.DiffHunk, target);
        }
        else
        {
            AppendHighlighted(builder, shown, SegmentRole.DiffHunk, highlight);
        }

        if (segment >= chunks.Count - 1 && builder.Remaining > 1)
        {
            builder.Append(" ", SegmentRole.Faint);
            builder.Append(new string(glyphs.Horizontal, builder.Remaining), SegmentRole.Faint);
        }

        return builder.Build(SegmentRole.Plain, isSelected);
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

    private static void AppendChange(
        LineBuilder builder,
        DiffRow row,
        int width,
        int gutterDigits,
        string? highlight,
        JumpLabel? jump,
        GraphGlyphSet? marks,
        IReadOnlyList<ChangedSpan>? changes)
    {
        var style = row.Kind switch
        {
            DiffRowKind.Added => new DiffRowStyle('+', SegmentRole.DiffAdded),
            DiffRowKind.Removed => new DiffRowStyle('-', SegmentRole.DiffRemoved),
            _ => new DiffRowStyle(' ', SegmentRole.Plain),
        };
        var role = style.Role;
        builder.Append(Gutter(row, gutterDigits), SegmentRole.Faint);
        if (!IsContent(row))
        {
            builder.Append(style.Marker.ToString(), role == SegmentRole.Plain ? SegmentRole.Faint : role);
        }

        AppendText(builder, row.Text, role, highlight, width, jump: jump, marks: marks, changes: changes);
    }

    private static void AppendText(
        LineBuilder builder,
        string text,
        SegmentRole role,
        string? highlight,
        int width,
        string prefix = "",
        JumpLabel? jump = null,
        GraphGlyphSet? marks = null,
        IReadOnlyList<ChangedSpan>? changes = null)
    {
        builder.Append(prefix, role);
        var cut = CutToWidth(text, width);
        var shown = Expand(cut, marks, cut.Length != text.Length, changes);
        if (jump is { } target)
        {
            JumpOverlay.Append(builder, shown.Text, role, target);
        }
        else
        {
            AppendHighlighted(builder, shown.Text, role, highlight, shown.Cells);
        }
    }

    /// <summary>
    /// Keeps the start of <paramref name="text"/> that can reach into <paramref name="width"/> columns (every character takes at
    /// least one column, so twice the width plus a margin for zero-width marks is enough), so a minified line of megabytes costs
    /// no more per draw than a short one. Still longer than the pane after expanding, so the ellipsis shows (not for pathological lines of mostly zero-width marks, which show cut without it).
    /// </summary>
    internal static string CutToWidth(string text, int width)
    {
        var limit = (Math.Max(width, 0) * 2) + CutMargin;
        if (text.Length <= limit)
        {
            return text;
        }

        return char.IsHighSurrogate(text[limit - 1]) ? text[..(limit - 1)] : text[..limit];
    }

    internal static void AppendHighlighted(
        LineBuilder builder,
        string shown,
        SegmentRole role,
        string? highlight,
        CellFlags[]? cells = null,
        int cellOffset = 0)
    {
        if (highlight is not { Length: > 0 })
        {
            AppendMarked(builder, shown, 0, shown.Length, role, cells, cellOffset);
            return;
        }

        var position = 0;
        while (position < shown.Length)
        {
            var found = shown.IndexOf(highlight, position, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                break;
            }

            AppendMarked(builder, shown, position, found, role, cells, cellOffset);
            builder.Append(shown.Substring(found, highlight.Length), SegmentRole.SearchMatch);
            position = found + highlight.Length;
        }

        AppendMarked(builder, shown, position, shown.Length, role, cells, cellOffset);
    }

    // Appends shown[start..end]; whitespace marks in it are drawn subtle and changed words on their tint.
    private static void AppendMarked(
        LineBuilder builder,
        string shown,
        int start,
        int end,
        SegmentRole role,
        CellFlags[]? cells,
        int cellOffset)
    {
        if (cells is null)
        {
            builder.Append(shown[start..end], role);
            return;
        }

        var runStart = start;
        while (runStart < end)
        {
            var flags = cells[cellOffset + runStart];
            var runEnd = runStart + 1;
            while (runEnd < end && cells[cellOffset + runEnd] == flags)
            {
                runEnd++;
            }

            builder.Append(shown[runStart..runEnd], RoleOf(role, flags));
            runStart = runEnd;
        }
    }

    // The role of a run: marks are subtle, changed words of an added or removed line sit on their tint.
    internal static SegmentRole RoleOf(SegmentRole role, CellFlags flags)
    {
        var isMark = flags.HasFlag(CellFlags.Mark);
        if (!flags.HasFlag(CellFlags.Changed))
        {
            return isMark ? SegmentRole.WhitespaceMark : role;
        }

        return role switch
        {
            SegmentRole.DiffAdded => isMark ? SegmentRole.WhitespaceMarkAddedWord : SegmentRole.DiffAddedWord,
            SegmentRole.DiffRemoved => isMark ? SegmentRole.WhitespaceMarkRemovedWord : SegmentRole.DiffRemovedWord,
            _ => isMark ? SegmentRole.WhitespaceMark : role,
        };
    }

    internal static string Number(int? value, int digits) =>
        value is { } number ? number.ToString(CultureInfo.InvariantCulture).PadLeft(digits) : new string(' ', digits);
}
