using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Mouse;

/// <summary>One row of the mouse map: <see cref="Gesture"/> on <see cref="View"/> runs <see cref="Action"/>, the same action a key would run.</summary>
/// <param name="View">View name such as <c>Graph</c>, <c>Tabs</c>, or <c>Lists</c> for every list pane.</param>
/// <param name="Gesture">What the user did.</param>
/// <param name="Action">The action to run.</param>
/// <param name="Count">The count handed to the action, like a typed <c>3</c> before a key; null when none.</param>
/// <param name="UsesTarget">The count is the number of the clicked item (for example the tab number) and <paramref name="Count"/> is ignored.</param>
public sealed record MouseBinding(string View, MouseGesture Gesture, ActionId Action, int? Count = null, bool UsesTarget = false);
