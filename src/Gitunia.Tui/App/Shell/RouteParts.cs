using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The second stage: what turns a key or a click into an action invoked in the state. The overlay state comes first
/// because the input command carries which overlay is open.
/// </summary>
public sealed class RouteParts
{
    /// <param name="foundation">The configuration, the state and the widgets.</param>
    public RouteParts(ShellFoundation foundation)
    {
        WhichKeyTimer = new WhichKeyTimer(OverlayController.WhichKeyDelay, foundation.Terminal.InvokeOnUi);
        FocusedView = new FocusedKeymapView(foundation.Host, foundation.Panes.Detail);
        Overlays = new OverlayStore(foundation.Host, foundation.Statuses, WhichKeyTimer, FocusedView);
        FocusSource = new InputFocusSource(Overlays, foundation.Panes);
        ActionInput = new ActionInput(foundation.Host, FocusSource);
    }

    /// <summary>Delays the which-key popup.</summary>
    public WhichKeyTimer WhichKeyTimer { get; }

    /// <summary>The keymap view of the focused pane.</summary>
    public FocusedKeymapView FocusedView { get; }

    /// <summary>The help, palette, menu, confirm dialog and output box.</summary>
    public OverlayStore Overlays { get; }

    /// <summary>The open overlay and the focused list.</summary>
    public InputFocusSource FocusSource { get; }

    /// <summary>Turns a key or a click into an action invoked in the state.</summary>
    public ActionInput ActionInput { get; }
}
