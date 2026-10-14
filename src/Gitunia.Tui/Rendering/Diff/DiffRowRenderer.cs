using System.Globalization;
using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>Draws one row of the diff pane: line number gutter, change marker and the text, cut to the width, or wrapped over several screen lines (<see cref="RenderSegment"/>).</summary>
public static class DiffRowRenderer
{
    /// <summary>Width of a tab stop; tabs in the diff are shown as spaces up to the next stop.</summary>
    public const int TabWidth = 4;

    private const int MinimumGutterDigits = 3;
    private const int CutMargin = 16;

    /// <summary>Digits each line number column needs for a document whose largest line number is <paramref name="maximumLineNumber"/>.</summary>
    public static int GutterDigits(int maximumLineNumber) =>
        Math.Max(MinimumGutterDigits, maximumLineNumber.ToString(CultureInfo.InvariantCulture).Length);

    /// <param name="row">The row.</param>
    /// <param name="width">Columns of the pane.</param>
    /// <param name="gutterDigits">Digits of each of the two line number columns, from <see cref="GutterDigits"/>.</param>
    /// <param name="highlight">Text to highlight (the diff search), null for none.</param>
    /// <param name="isSelected">The row is under the cursor.</param>
    /// <param name="glyphs">Supplies the ellipsis.</param>
    public static StyledLine Render(DiffRow row, int width, int gutterDigits, string? highlight, bool isSelected, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        switch (row.Kind)
        {
            case DiffRowKind.FileHeader:
                AppendText(builder, row.Text, SegmentRole.DiffFile, highlight, width);
                break;
            case DiffRowKind.HunkHeader:
                AppendText(builder, row.Text, SegmentRole.DiffHunk, highlight, width);
                break;
            case DiffRowKind.Warning:
                AppendText(builder, $"! {row.Text}", SegmentRole.Error, highlight, width);
                break;
            case DiffRowKind.Note:
            case DiffRowKind.NoNewline:
                AppendText(builder, $"  {row.Text}", SegmentRole.Faint, highlight, width);
                break;
            default:
                AppendChange(builder, row, width, gutterDigits, highlight);
                break;
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>Columns taken before the text of <paramref name="row"/> (line numbers and marker, or the note prefix).</summary>
    public static int PrefixWidth(DiffRow row, int gutterDigits) => row.Kind switch
    {
        DiffRowKind.FileHeader or DiffRowKind.HunkHeader => 0,
        DiffRowKind.Warning or DiffRowKind.Note or DiffRowKind.NoNewline => 2,
        _ => (2 * gutterDigits) + 3,
    };

    /// <summary>How many screen lines <paramref name="row"/> takes when its text wraps at <paramref name="width"/> columns.</summary>
    public static int SegmentCount(DiffRow row, int width, int gutterDigits)
    {
        var columns = width - PrefixWidth(row, gutterDigits);
        return columns <= 0 || (row.Text.Length * 2 <= columns && !ContainsControl(row.Text)) ? 1 : Chunks(ExpandTabs(row.Text), columns).Count;
    }

    /// <summary>Cuts <paramref name="shown"/> into pieces of at most <paramref name="columns"/> columns; a wide character is never split.</summary>
    public static List<string> Chunks(string shown, int columns)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        var used = 0;
        foreach (var rune in shown.EnumerateRunes())
        {
            var runeWidth = TextWidth.Of(rune);
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
    public static StyledLine RenderSegment(DiffRow row, int segment, int width, int gutterDigits, string? highlight, bool isSelected, GraphGlyphSet glyphs)
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
            return Render(row, width, gutterDigits, highlight, isSelected, glyphs);
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
                case DiffRowKind.Context or DiffRowKind.Added or DiffRowKind.Removed:
                    builder.Append($"{Number(row.OldLineNumber, gutterDigits)} {Number(row.NewLineNumber, gutterDigits)} ", SegmentRole.Faint);
                    var marker = row.Kind == DiffRowKind.Added ? "+" : row.Kind == DiffRowKind.Removed ? "-" : " ";
                    builder.Append(marker, textRole == SegmentRole.Plain ? SegmentRole.Faint : textRole);
                    break;
            }
        }
        else
        {
            builder.Append(new string(' ', prefixWidth), SegmentRole.Plain);
        }

        var chunks = Chunks(ExpandTabs(row.Text), columns);
        AppendHighlighted(builder, segment < chunks.Count ? chunks[segment] : string.Empty, textRole, highlight);
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
                column += TextWidth.Of(rune);
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

    private static void AppendChange(LineBuilder builder, DiffRow row, int width, int gutterDigits, string? highlight)
    {
        var style = row.Kind switch
        {
            DiffRowKind.Added => new DiffRowStyle('+', SegmentRole.DiffAdded),
            DiffRowKind.Removed => new DiffRowStyle('-', SegmentRole.DiffRemoved),
            _ => new DiffRowStyle(' ', SegmentRole.Plain),
        };
        var role = style.Role;
        var gutter = $"{Number(row.OldLineNumber, gutterDigits)} {Number(row.NewLineNumber, gutterDigits)} ";
        builder.Append(gutter, SegmentRole.Faint);
        builder.Append(style.Marker.ToString(),role == SegmentRole.Plain ? SegmentRole.Faint : role);
        AppendText(builder, row.Text, role, highlight, width);
    }

    private static void AppendText(LineBuilder builder, string text, SegmentRole role, string? highlight, int width)
    {
        AppendHighlighted(builder, ExpandTabs(CutToWidth(text, width)), role, highlight);
    }

    /// <summary>
    /// Keeps the start of <paramref name="text"/> that can reach into <paramref name="width"/> columns (every character takes at
    /// least one column, so twice the width plus a margin for zero-width marks is enough), so a minified line of megabytes costs
    /// no more per draw than a short one. Still longer than the pane after expanding, so the ellipsis shows (not for pathological lines of mostly zero-width marks, which show cut without it).
    /// </summary>
    private static string CutToWidth(string text, int width)
    {
        var limit = (Math.Max(width, 0) * 2) + CutMargin;
        if (text.Length <= limit)
        {
            return text;
        }

        return char.IsHighSurrogate(text[limit - 1]) ? text[..(limit - 1)] : text[..limit];
    }

    private static void AppendHighlighted(LineBuilder builder, string shown, SegmentRole role, string? highlight)
    {
        if (highlight is not { Length: > 0 })
        {
            builder.Append(shown, role);
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

            builder.Append(shown[position..found], role);
            builder.Append(shown.Substring(found, highlight.Length), SegmentRole.SearchMatch);
            position = found + highlight.Length;
        }

        builder.Append(shown[position..], role);
    }

    private static string Number(int? value, int digits) =>
        value is { } number ? number.ToString(CultureInfo.InvariantCulture).PadLeft(digits) : new string(' ', digits);
}
