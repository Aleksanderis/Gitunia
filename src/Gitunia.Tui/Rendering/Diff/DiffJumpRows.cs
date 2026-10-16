using Gitunia.Core.Diff;
using Gitunia.Core.Jump;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The rows of a diff as quick-jump rows: the text after the line numbers and marker, as
/// <see cref="DiffRowRenderer"/> shows it. Side by side (a <paramref name="split"/> is given) a row sits in its half of
/// the pane: a label must fit there, and only the rows of the split lines <paramref name="firstLine"/> to
/// <paramref name="lastLine"/> (those on screen) have text to look through.
/// </summary>
/// <param name="document">The diff.</param>
/// <param name="width">Columns of the pane's text area.</param>
/// <param name="gutterDigits">Digits of the line number columns.</param>
/// <param name="ellipsis">The ellipsis a cut row ends with.</param>
/// <param name="split">The side-by-side arrangement, null for the unified view.</param>
/// <param name="firstLine">First split line in view.</param>
/// <param name="lastLine">Last split line in view.</param>
public sealed class DiffJumpRows(
    DiffDocument document,
    int width,
    int gutterDigits,
    string ellipsis,
    DiffSplit? split = null,
    int firstLine = 0,
    int lastLine = -1) : IJumpRows
{
    public int Count => document.Rows.Count;

    public int EllipsisWidth { get; } = TextWidth.Of(ellipsis);

    public string TextOf(int row)
    {
        if (split is not null && !IsInView(row))
        {
            return string.Empty;
        }

        return DiffRowRenderer.ExpandTabs(DiffRowRenderer.CutToWidth(document.Rows[row].Text, width));
    }

    public int ColumnsOf(int row)
    {
        if (split is null || split.Lines[split.LineOfRow[row]].IsFull)
        {
            return Math.Max(0, width - DiffRowRenderer.PrefixWidth(document.Rows[row], gutterDigits));
        }

        var line = split.Lines[split.LineOfRow[row]];
        var half = line.LeftRow == row ? DiffSplitRenderer.LeftWidth(width) : DiffSplitRenderer.RightWidth(width);
        return Math.Max(0, half - DiffSplitRenderer.SidePrefixWidth(gutterDigits));
    }

    private bool IsInView(int row)
    {
        var line = split!.LineOfRow[row];
        return line >= firstLine && line <= lastLine;
    }
}
