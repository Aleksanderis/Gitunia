using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Files;

/// <summary>
/// The <c>view ▾</c> chip in the title row of a file list (the Changes and Files tabs, the two working-tree lists)
/// that opens the dropdown of the list's view options (#646; the path style since #540). Like the diff's chips it
/// shows its key before the label when there is room, and the same layout draws it and finds it under a click.
/// </summary>
public static class ViewChip
{
    private const string Description = "view ▾";

    // A column of line stays between the title text and the chip.
    private const int TitleGap = 1;

    /// <summary>The chip, or null when the row has no room for it after the title text.</summary>
    /// <param name="mouseView">The mouse view of the chip, which names its action.</param>
    /// <param name="keyView">The keymap view the chip sits in, where its key text comes from.</param>
    /// <param name="titleColumns">Columns the title text of the row takes from its start.</param>
    /// <param name="rightEdge">The column the chip must end before.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static TitleButton? Layout(string mouseView, string keyView, int titleColumns, int rightEdge, ChipKeys? keys)
    {
        var key = (keys ?? ChipKeys.Default).For(mouseView, keyView);
        var keyed = key is null ? null : KeyedChip.Keyed(mouseView, Description, key);
        var plain = KeyedChip.Plain(mouseView, Description);
        foreach (var candidate in (TitleButton?[])[keyed, plain])
        {
            if (candidate is not null && rightEdge - candidate.Width >= titleColumns + TitleGap)
            {
                return candidate with { Start = rightEdge - candidate.Width };
            }
        }

        return null;
    }

    /// <summary>The chip as drawn: its key in the key style, the rest like the tabs; in the ASCII tier with
    /// brackets.</summary>
    public static StyledLine Line(TitleButton button, GraphGlyphSet glyphs) => new(KeyedChip.Segments(button, glyphs));
}
