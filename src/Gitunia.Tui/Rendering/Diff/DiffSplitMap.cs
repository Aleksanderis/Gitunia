using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// Maps the rows of a diff to the screen lines of the side-by-side view: a removed and an added row share a line, so several
/// rows can start on the same screen line. With word wrap on a line takes as many screen lines as its longer side needs.
/// </summary>
public sealed class DiffSplitMap
{
    private readonly int[] _starts;

    private DiffSplitMap(DiffDocument document, DiffSplit split, int width, bool isWrapped, int[] starts)
    {
        Document = document;
        Split = split;
        Width = width;
        IsWrapped = isWrapped;
        _starts = starts;
    }

    public DiffDocument Document { get; }

    public DiffSplit Split { get; }

    public int Width { get; }

    public bool IsWrapped { get; }

    /// <summary>Screen lines in all.</summary>
    public int Count => _starts[^1];

    /// <param name="document">The diff.</param>
    /// <param name="split">The arrangement of its rows, from <see cref="DiffSplitLayout.Build"/>.</param>
    /// <param name="width">Columns of the pane.</param>
    /// <param name="isWrapped">Long text continues on further screen lines instead of being cut.</param>
    public static DiffSplitMap Build(DiffDocument document, DiffSplit split, int width, bool isWrapped)
    {
        var starts = new int[split.Lines.Count + 1];
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        for (var index = 0; index < split.Lines.Count; index++)
        {
            var segments = isWrapped ? DiffSplitRenderer.SegmentCount(document, split.Lines[index], width, digits) : 1;
            starts[index + 1] = starts[index] + segments;
        }

        return new DiffSplitMap(document, split, width, isWrapped, starts);
    }

    /// <summary>The same arrangement at another pane width.</summary>
    public DiffSplitMap Resize(int width) => width == Width ? this : Build(Document, Split, width, IsWrapped);

    /// <summary>First screen line of <paramref name="row"/>.</summary>
    public int StartOf(int row) =>
        Split.LineOfRow.Count == 0 ? 0 : _starts[Split.LineOfRow[Math.Clamp(row, 0, Split.LineOfRow.Count - 1)]];

    /// <summary>The first row shown on screen line <paramref name="line"/>.</summary>
    public int RowOf(int line) => Split.Lines.Count == 0 ? 0 : Split.Lines[SplitLineOf(line)].FirstRow;

    /// <summary>The last row shown on screen line <paramref name="line"/>.</summary>
    public int LastRowOf(int line) => Split.Lines.Count == 0 ? 0 : Split.Lines[SplitLineOf(line)].LastRow;

    /// <summary>Index into <see cref="DiffSplit.Lines"/> of the line shown on screen line <paramref name="line"/>.</summary>
    public int SplitLineOf(int line)
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

    /// <summary>First screen line of the split line <paramref name="index"/>.</summary>
    public int StartOfSplitLine(int index) => _starts[index];
}
