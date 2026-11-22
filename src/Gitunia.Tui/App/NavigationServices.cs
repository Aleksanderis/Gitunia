using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App;

/// <summary>What the graph and detail actions use besides the lists.</summary>
/// <param name="Quit">Asks the application to quit.</param>
/// <param name="Geometry">The rows the panes are laid out in, which the banner row shortens.</param>
/// <param name="Statuses">The status actions, whose fallback takes Esc.</param>
/// <param name="Clipboard">Copies a commit hash.</param>
public sealed record NavigationServices(
    IQuitRequests Quit,
    PaneGeometry Geometry,
    StatusActionHandler Statuses,
    ClipboardCopier Clipboard);
