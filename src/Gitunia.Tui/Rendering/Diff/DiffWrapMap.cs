using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Core.Text;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// Maps the rows of a diff to the screen lines they take when text wraps at a given pane width.
/// The per-row text widths are measured once per document and shared by every width.
/// A resize only does arithmetic for rows whose every character takes one column once tabs are expanded (ASCII, tab-indented, accented or other narrow text); rows with wide or zero-width characters also need no chunking while they fit one line, and only longer ones keep the exact slow path.
/// </summary>
public sealed class DiffWrapMap
{
    private readonly int[] _starts;
    private readonly int[] _textColumns;

    private DiffWrapMap(DiffDocument document, int width, int[] starts, int[] textColumns)
    {
        Document = document;
        Width = width;
        _starts = starts;
        _textColumns = textColumns;
    }

    public DiffDocument Document { get; }

    public int Width { get; }

    /// <summary>Screen lines in all.</summary>
    public int Count => _starts[^1];

    public static DiffWrapMap Build(DiffDocument document, int width)
    {
        var textColumns = new int[document.Rows.Count];
        for (var index = 0; index < textColumns.Length; index++)
        {
            textColumns[index] = DisplayColumns(document.Rows[index].Text);
        }

        return Layout(document, width, textColumns);
    }

    /// <summary>The same document at another pane width, reusing the measured row widths instead of scanning every row's text again.</summary>
    public DiffWrapMap Resize(int width) => width == Width ? this : Layout(Document, width, _textColumns);

    /// <summary>First screen line of <paramref name="row"/>.</summary>
    public int StartOf(int row) => _starts[Math.Clamp(row, 0, _starts.Length - 1)];

    /// <summary>The row shown on screen line <paramref name="line"/>.</summary>
    public int RowOf(int line)
    {
        var low = 0;
        var high = _starts.Length - 2;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_starts[middle] <= line)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return Math.Max(0, low);
    }

    private static DiffWrapMap Layout(DiffDocument document, int width, int[] textColumns)
    {
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        var starts = new int[textColumns.Length + 1];
        for (var index = 0; index < textColumns.Length; index++)
        {
            var row = document.Rows[index];
            var length = textColumns[index];
            var columns = width - DiffRowRenderer.PrefixWidth(row, digits);
            int segments;
            if (columns <= 0)
            {
                segments = 1;
            }
            else if (length >= 0)
            {
                segments = Math.Max(1, (length + columns - 1) / columns);
            }
            else if (~length <= columns)
            {
                segments = 1;
            }
            else
            {
                segments = DiffRowRenderer.SegmentCount(row, width, digits);
            }

            starts[index + 1] = starts[index] + segments;
        }

        return new DiffWrapMap(document, width, starts, textColumns);
    }

    /// <summary>
    /// The columns <paramref name="text"/> takes once tabs are expanded. Non-negative when every character is one column wide
    /// (wrapping is then plain division); otherwise the bitwise complement of the width (a wide or zero-width character needs the
    /// full chunking logic unless the whole text fits one line).
    /// </summary>
    private static int DisplayColumns(string text)
    {
        var column = 0;
        var simple = true;
        var asciiLength = 0;
        while (asciiLength < text.Length && text[asciiLength] is >= ' ' and <= '~')
        {
            asciiLength++;
        }

        if (asciiLength == text.Length)
        {
            return asciiLength;
        }

        var counter = new WidthCounter();
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\t')
            {
                column += DiffRowRenderer.TabWidth - (column % DiffRowRenderer.TabWidth);
            }
            else if (rune.Value is >= ' ' and < 0x7F || Rune.IsControl(rune))
            {
                column++;
            }
            else
            {
                var runeWidth = counter.Next(rune);
                simple &= runeWidth == 1;
                column += runeWidth;
            }
        }

        return simple ? column : ~column;
    }
}
