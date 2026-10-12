using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;

namespace Gitunia.Tui.Input;

/// <summary>
/// Application-level mouse handling, the counterpart of <see cref="KeyController"/>: a gesture on a view is looked up in the
/// <see cref="MouseMap"/> and the action it names is handed to <paramref name="execute"/>, the same delegate the keys use.
/// </summary>
/// <param name="map">The gesture bindings.</param>
/// <param name="execute">Runs an action id with its count.</param>
/// <param name="isSuppressed">True while something modal (an overlay) owns the screen and pane gestures must not act behind it.</param>
public sealed class MouseController(MouseMap map, Action<ActionId, int?> execute, Func<bool> isSuppressed)
{
    /// <param name="view">The view the pointer was on (a keymap view name, or <see cref="DefaultMouseMap.TabsView"/>).</param>
    /// <param name="gesture">What the user did.</param>
    /// <param name="target">The number of the clicked item for bindings that use it (the tab number), null otherwise.</param>
    /// <returns>True when the gesture ran an action.</returns>
    public bool Handle(string view, MouseGesture gesture, int? target = null)
    {
        if (isSuppressed() || map.Resolve(view, gesture) is not { } binding)
        {
            return false;
        }

        execute(binding.Action, binding.UsesTarget ? target : binding.Count);
        return true;
    }
}
