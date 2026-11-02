using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Settings;

namespace Gitunia.Tui.App.Shell;

/// <summary>The views that place themselves again when the window changes size.</summary>
/// <param name="DiffPane">Follows the new size.</param>
/// <param name="Settings">Places the settings screen again.</param>
/// <param name="Overlays">Closes the actions menu, which hangs on a row of the old layout.</param>
/// <param name="Popups">Draws the prompt popup again.</param>
public sealed record ResizeTargets(
    DiffPaneInput DiffPane,
    SettingsScreenInput Settings,
    OverlayController Overlays,
    PromptPopupTracker Popups);
