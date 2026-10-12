using Gitunia.Core.Lists;

namespace Gitunia.Tui.Views;

/// <summary>A list source that draws the rows of a Visual-mode selection like the selected row.</summary>
public interface IRangeSource
{
    /// <summary>The rows (list items) of the range selection, null when none is active.</summary>
    RowRange? Range { get; set; }
}
