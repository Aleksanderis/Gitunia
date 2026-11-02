using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>A list source that draws buttons over its rows.</summary>
public interface IButtonSource
{
    /// <summary>Tells which button, if any, is drawn at a column and row of the list.</summary>
    /// <param name="listView">The list that shows the rows.</param>
    /// <param name="column">The column in the list.</param>
    /// <param name="row">The row in the list.</param>
    /// <returns>The button's view name, null for none.</returns>
    string? ButtonAt(ListView listView, int column, int row);
}
