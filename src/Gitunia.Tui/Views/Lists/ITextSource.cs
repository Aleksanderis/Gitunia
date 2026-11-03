using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Tui.Rendering.Diff;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>
/// A list source whose items are lines of plain text, so a pointer position maps to a character (character-wise selection).
/// Positions are an item (a screen line) and the index of a character in the text of the row the item shows; in the
/// side-by-side diff a selection follows one side.
/// </summary>
public interface ITextSource
{
    /// <summary>
    /// The characters of a character-wise selection inside the <see cref="IRangeSource.Range"/> rows, drawn instead of the whole rows;
    /// null for a row-wise selection.
    /// </summary>
    TextSelection? Text { get; set; }

    /// <summary>The selection is on the left (old) side of the side-by-side diff.</summary>
    bool IsOldSide { get; set; }

    /// <summary>Character columns are meaningful right now (there are rows and they show text).</summary>
    bool SupportsText { get; }

    /// <summary>The text of the row item <paramref name="item"/> shows (on a side, side by side), empty when there is none.</summary>
    /// <param name="item">The item.</param>
    /// <param name="isOldSide">The left side of the side-by-side diff.</param>
    string TextOf(int item, bool isOldSide);

    /// <summary>The character under a pointer position.</summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="column">The pointer column inside the list view.</param>
    /// <param name="row">The pointer row inside the list view; above or below the rows it is clamped to the first or last.</param>
    /// <param name="isOldSide">Stay on this side of the side-by-side diff; null takes the side the pointer is on.</param>
    /// <returns>The hit, null when there are no rows.</returns>
    TextHit? HitAt(ListView listView, int column, int row, bool? isOldSide);

    /// <summary>Where a selection starts at the cursor item: the first character the item shows.</summary>
    /// <param name="item">The item.</param>
    /// <param name="isOldSide">The left side of the side-by-side diff.</param>
    TextPosition StartAt(int item, bool isOldSide);

    /// <summary>The item that shows the character at <paramref name="position"/> (a wrapped row spreads over several items).</summary>
    /// <param name="position">The position.</param>
    /// <param name="isOldSide">The left side of the side-by-side diff.</param>
    int ItemShowing(TextPosition position, bool isOldSide);

    /// <summary>The characters a selection covers, as they are copied.</summary>
    /// <param name="selection">The selection.</param>
    /// <param name="isOldSide">The left side of the side-by-side diff.</param>
    string CopyText(TextSelection selection, bool isOldSide);
}
