using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>A list source whose rows are lines of plain text, so a pointer position maps to a character (character-wise selection).</summary>
public interface ITextSource : ITextLines
{
    /// <summary>
    /// The characters of a character-wise selection inside the <see cref="IRangeSource.Range"/> rows, drawn instead of the whole rows;
    /// null for a row-wise selection.
    /// </summary>
    TextSelection? Text { get; set; }

    /// <summary>Rows are single screen lines of text right now (not wrapped, not side by side), so character columns are meaningful.</summary>
    bool SupportsText { get; }

    /// <summary>The character under a pointer position.</summary>
    /// <param name="listView">The list this source is shown in.</param>
    /// <param name="column">The pointer column inside the list view.</param>
    /// <param name="row">The pointer row inside the list view; above or below the rows it is clamped to the first or last.</param>
    /// <returns>The position, null when there are no rows.</returns>
    TextPosition? PositionAt(ListView listView, int column, int row);
}
