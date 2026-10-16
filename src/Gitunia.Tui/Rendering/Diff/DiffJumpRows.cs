using Gitunia.Core.Diff;
using Gitunia.Core.Jump;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The rows of a diff as quick-jump rows: the text after the line numbers and marker, as <see cref="DiffRowRenderer"/> shows it.</summary>
/// <param name="document">The diff.</param>
/// <param name="width">Columns of the pane's text area.</param>
/// <param name="gutterDigits">Digits of the line number columns.</param>
/// <param name="ellipsis">The ellipsis a cut row ends with.</param>
public sealed class DiffJumpRows(DiffDocument document, int width, int gutterDigits, string ellipsis) : IJumpRows
{
    public int Count => document.Rows.Count;

    public int EllipsisWidth { get; } = TextWidth.Of(ellipsis);

    public string TextOf(int row) => DiffRowRenderer.ExpandTabs(DiffRowRenderer.CutToWidth(document.Rows[row].Text, width));

    public int ColumnsOf(int row) => Math.Max(0, width - DiffRowRenderer.PrefixWidth(document.Rows[row], gutterDigits));
}
