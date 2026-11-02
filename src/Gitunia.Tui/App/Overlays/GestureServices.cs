using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>What the mouse gestures on the panes read to decide whether an overlay takes them.</summary>
/// <param name="Guard">Ignores a double-click that follows a click on an overlay.</param>
/// <param name="Cover">Which overlays are open.</param>
/// <param name="Editors">The editors and the settings screen.</param>
/// <param name="Panes">The widgets, for the overlay box and the chrome to redraw.</param>
public sealed record GestureServices(
    OverlayClickGuard Guard,
    OverlayCover Cover,
    EditorOverlays Editors,
    PaneViews Panes);
