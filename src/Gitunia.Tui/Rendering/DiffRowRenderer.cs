using System.Globalization;
using System.Text;
using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering;

/// <summary>Draws one row of the diff pane: line number gutter, change marker and the text, cut to the width. Text never wraps.</summary>
public static class DiffRowRenderer
{
    /// <summary>Width of a tab stop; tabs in the diff are shown as spaces up to the next stop.</summary>
    public const int TabWidth = 4;

    private const int MinimumGutterDigits = 3;

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
                AppendText(builder, row.Text, SegmentRole.DiffFile, highlight);
                break;
            case DiffRowKind.HunkHeader:
                AppendText(builder, row.Text, SegmentRole.DiffHunk, highlight);
                break;
            case DiffRowKind.Warning:
                AppendText(builder, "! " + row.Text, SegmentRole.Error, highlight);
                break;
            case DiffRowKind.Note:
            case DiffRowKind.NoNewline:
                AppendText(builder, "  " + row.Text, SegmentRole.Faint, highlight);
                break;
            default:
                AppendChange(builder, row, gutterDigits, highlight);
                break;
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

    private static void AppendChange(LineBuilder builder, DiffRow row, int gutterDigits, string? highlight)
    {
        var (marker, role) = row.Kind switch
        {
            DiffRowKind.Added => ('+', SegmentRole.DiffAdded),
            DiffRowKind.Removed => ('-', SegmentRole.DiffRemoved),
            _ => (' ', SegmentRole.Plain),
        };
        var gutter = Number(row.OldLineNumber, gutterDigits) + " " + Number(row.NewLineNumber, gutterDigits) + " ";
        builder.Append(gutter, SegmentRole.Faint);
        builder.Append(marker.ToString(), role == SegmentRole.Plain ? SegmentRole.Faint : role);
        AppendText(builder, row.Text, role, highlight);
    }

    private static void AppendText(LineBuilder builder, string text, SegmentRole role, string? highlight)
    {
        var shown = ExpandTabs(text);
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
