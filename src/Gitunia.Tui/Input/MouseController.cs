using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.App;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.Input;

/// <summary>
/// Application-level mouse handling, the counterpart of <see cref="KeyController"/>: a gesture on a view is looked up in the
/// <see cref="MouseMap"/> and the action it names is handed to <paramref name="route"/>, the same route the keys use.
/// </summary>
/// <param name="map">The gesture bindings.</param>
/// <param name="route">Runs an action id with its count.</param>
/// <param name="gate">Tells whether something modal (an overlay) owns the screen, so pane gestures must not act behind it (the overlays' own views still do).</param>
public sealed class MouseController(MouseMap map, IActionRoute route, IMouseGate gate)
{
    /// <summary>Runs the action of a hint or an action row that was clicked, as if its key had been pressed.</summary>
    /// <param name="action">The action the hint stands for.</param>
    public void Run(ActionId action) => route.Route(action, null);

    /// <param name="view">The view the pointer was on (a keymap view name, or <see cref="DefaultMouseMap.TabsView"/>).</param>
    /// <param name="gesture">What the user did.</param>
    /// <param name="target">The number of the clicked item for bindings that use it (the tab number), null otherwise.</param>
    /// <returns>True when the gesture ran an action.</returns>
    public bool Handle(string view, MouseGesture gesture, int? target = null)
    {
        // An open overlay hides the panes from the mouse, but its own views (and nothing else) still take gestures.
        if ((gate.IsSuppressed && !DefaultMouseMap.OverlayViews.Contains(view)) || map.Resolve(view, gesture) is not { } binding)
        {
            return false;
        }

        // The first click of a double-click already toggled a folder row; the double-click must not leave that
        // fold changed (#622).
        if (gesture == MouseGesture.DoubleClick
            && map.Resolve(view, MouseGesture.Click) is { } click
            && (click.Action == ActionIds.DetailToggleFolder || click.Action == ActionIds.WorkingTreeToggleFolder))
        {
            route.Route(click.Action, click.Count);
        }

        route.Route(binding.Action, binding.UsesTarget ? target : binding.Count);
        return true;
    }
}
