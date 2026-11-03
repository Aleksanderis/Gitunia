using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;

namespace Gitunia.Core.Blame;

/// <summary>
/// The range selected in the blame view: whole lines or characters of the code column. It starts at
/// <see cref="AnchorLine"/> and ends at the cursor line of the view, in either direction. Pure data.
/// </summary>
/// <param name="Kind">Whole lines or characters.</param>
/// <param name="AnchorLine">The line the selection started on.</param>
/// <param name="AnchorColumn">The column in the anchor line where a character selection started.</param>
/// <param name="CursorColumn">The column in the cursor line where a character selection ends.</param>
public sealed record BlameSelection(VisualKind Kind, int AnchorLine, int AnchorColumn = 0, int CursorColumn = 0)
{
    /// <summary>The rows the selection covers when the cursor is on <paramref name="cursor"/>.</summary>
    /// <param name="cursor">The cursor line of the view.</param>
    public RowRange RangeTo(int cursor) => new(AnchorLine, cursor);

    /// <summary>The character selection when the cursor is on <paramref name="cursor"/>, null for whole lines.</summary>
    /// <param name="cursor">The cursor line of the view.</param>
    public TextSelection? TextTo(int cursor) => Kind == VisualKind.Chars
        ? new TextSelection(new TextPosition(AnchorLine, AnchorColumn), new TextPosition(cursor, CursorColumn))
        : null;
}
