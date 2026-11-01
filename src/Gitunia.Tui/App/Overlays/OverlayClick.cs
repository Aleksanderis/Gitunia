namespace Gitunia.Tui.App.Overlays;

/// <summary>What a click on an overlay line came to: the overlay that took it and the mouse view that finishes the job.</summary>
/// <param name="View">The mouse view whose action follows the click (a chip, a row, a button), null when the click only selected.</param>
/// <param name="Target">The number of the clicked item for views that use one (a help tab, a parent row), null otherwise.</param>
/// <param name="GuardsDoubleClick">True when the click starts the short window in which a double-click is ignored.</param>
public sealed record OverlayClick(string? View, int? Target = null, bool GuardsDoubleClick = true);
