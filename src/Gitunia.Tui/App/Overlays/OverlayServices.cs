using Gitunia.App.Status;
using Gitunia.Core.Keymap;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>What the overlay controller reads and uses to build and show its overlays.</summary>
/// <param name="Engine">The keymap engine (its table feeds the help and the palette).</param>
/// <param name="Chrome">The terminal size.</param>
/// <param name="Config">The command level and the which-key setting, read each time.</param>
/// <param name="Menus">What the actions menu of a row offers.</param>
/// <param name="Statuses">The status messages overlays post.</param>
public sealed record OverlayServices(
    KeymapEngine Engine,
    IPaneChrome Chrome,
    LiveConfig Config,
    IRowMenuTargets Menus,
    StatusReporter Statuses);
