using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Visual;

/// <summary>
/// The Visual-mode selection that is active: a range of rows anchored at the cursor of one pane of one tab, whole rows or,
/// for <see cref="VisualKind.Chars"/>, the characters between a position in the first row and one in the last.
/// </summary>
/// <param name="TabId">The tab whose pane the selection belongs to.</param>
/// <param name="Pane">The pane the selection is drawn in.</param>
/// <param name="Range">The rows it covers.</param>
/// <param name="Kind">Whole rows or characters.</param>
/// <param name="AnchorColumn">The column in the anchor row where a character-wise selection started.</param>
/// <param name="CursorColumn">The column in the cursor row where its moving end is.</param>
public sealed record VisualSelection(
    TabId TabId,
    PaneKind Pane,
    RowRange Range,
    VisualKind Kind = VisualKind.Lines,
    int AnchorColumn = 0,
    int CursorColumn = 0)
{
    /// <summary>The character-wise selection, null for a row-wise one.</summary>
    public TextSelection? Text => Kind == VisualKind.Chars
        ? new TextSelection(new TextPosition(Range.Anchor, AnchorColumn), new TextPosition(Range.Cursor, CursorColumn))
        : null;
}
