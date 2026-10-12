using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering;

/// <summary>Maps the rows of a diff to the screen lines they take when text wraps at a given pane width.</summary>
public sealed class DiffWrapMap
{
    private readonly int[] starts;

    private DiffWrapMap(DiffDocument document, int width, int[] starts)
    {
        Document = document;
        Width = width;
        this.starts = starts;
    }

    public DiffDocument Document { get; }

    public int Width { get; }

    /// <summary>Screen lines in all.</summary>
    public int Count => starts[^1];

    public static DiffWrapMap Build(DiffDocument document, int width)
    {
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);
        var starts = new int[document.Rows.Count + 1];
        for (var index = 0; index < document.Rows.Count; index++)
        {
            starts[index + 1] = starts[index] + DiffRowRenderer.SegmentCount(document.Rows[index], width, digits);
        }

        return new DiffWrapMap(document, width, starts);
    }

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
}
