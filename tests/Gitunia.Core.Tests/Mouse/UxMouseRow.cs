using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;

namespace Gitunia.Core.Tests.Mouse;

/// <summary>One row of the mouse table in <c>docs/ux.md</c>.</summary>
/// <param name="View">The view the gesture applies to.</param>
/// <param name="Gesture">The gesture.</param>
/// <param name="Action">The action it runs.</param>
public sealed record UxMouseRow(string View, MouseGesture Gesture, ActionId Action);
