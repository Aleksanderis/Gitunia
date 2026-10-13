using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering;

/// <summary>
/// Maps the rows of a diff to the screen lines they take when text wraps at a given pane width.
/// The per-row text widths are measured once per document and shared by every width, so a resize only does arithmetic for printable-ASCII rows (the slow path stays for the rest).
/// </summary>
public sealed class DiffWrapMap
{
    private readonly int[] starts;
    private readonly int[] textColumns;

    private DiffWrapMap(DiffDocument document, int width, int[] starts, int[] textColumns)
    {
        Document = document;
        Width = width;
        this.starts = starts;
        this.textColumns = textColumns;
    }

    public DiffDocument Document { get; }

    public int Width { get; }

    /// <summary>Screen lines in all.</summary>
    public int Count => starts[^1];

    public static DiffWrapMap Build(DiffDocument document, int width)
    {
        var textColumns = new int[document.Rows.Count];
        for (var index = 0; index < textColumns.Length; index++)
        {
            textColumns[index] = PrintableAsciiLength(document.Rows[index].Text);
        }

        return Layout(document, width, textColumns);
    }

    /// <summary>The same document at another pane width, reusing the measured row widths instead of scanning every row's text again.</summary>
    public DiffWrapMap Resize(int width) => width == Width ? this : Layout(Document, width, textColumns);

    /// <summary>First screen line of <paramref name="row"/>.</summary>
    public int StartOf(int row) => starts[Math.Clamp(row, 0, starts.Length - 1)];

    /// <summary>The row shown on screen line <paramref name="line"/>.</summary>
    public int RowOf(int line)
    {
        var low = 0;
        var high = starts.Length - 2;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (starts[middle] <= line)
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
            int segments;
            if (length < 0)
            {
                segments = DiffRowRenderer.SegmentCount(row, width, digits);
            }
            else
            {
                var columns = width - DiffRowRenderer.PrefixWidth(row, digits);
                segments = columns <= 0 ? 1 : Math.Max(1, (length + columns - 1) / columns);
            }

            starts[index + 1] = starts[index] + segments;
        }

        return new DiffWrapMap(document, width, starts, textColumns);
    }

    /// <summary>The length of <paramref name="text"/> when every character is a one-column printable ASCII one, otherwise -1 (the text needs the full wrapping logic).</summary>
    private static int PrintableAsciiLength(string text)
    {
        foreach (var character in text)
        {
            if (character is < ' ' or > '~')
            {
                return -1;
            }
        }

        return text.Length;
    }
}
