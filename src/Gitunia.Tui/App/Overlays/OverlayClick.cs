namespace Gitunia.Tui.App.Overlays;

/// <summary>What a click on an overlay line came to: the mouse view of the overlay that took it.</summary>
/// <param name="View">The mouse view whose action follows the click (a chip, a row, a button), null if none.</param>
/// <param name="Target">The number of the clicked item for views that use one (a help tab), null otherwise.</param>
/// <param name="GuardsDoubleClick">True when the click starts the short window that ignores a double-click.</param>
public sealed record OverlayClick(string? View, int? Target = null, bool GuardsDoubleClick = true)
{
    /// <summary>The click an editor reports: its mouse view when it took the click, nothing when it did not.</summary>
    /// <param name="view">The mouse view the editor named, null if none.</param>
    /// <param name="isHandled">True when the editor took the click.</param>
    public static OverlayClick? OfEditor(string? view, bool isHandled) => isHandled ? new OverlayClick(view) : null;
}
