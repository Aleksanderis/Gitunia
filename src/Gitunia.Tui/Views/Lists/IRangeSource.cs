using Gitunia.Core.Lists;

namespace Gitunia.Tui.Views.Lists;

/// <summary>A list source that draws the rows of a Visual-mode selection like the selected row.</summary>
public interface IRangeSource
{
    /// <summary>The rows (list items) of the range selection, null when none is active.</summary>
    RowRange? Range { get; set; }

    /// <summary>Raised when the rows the range refers to changed meaning (new content, wrap toggled, width changed); the range is no longer valid.</summary>
    event Action? RangeInvalidated;
}
