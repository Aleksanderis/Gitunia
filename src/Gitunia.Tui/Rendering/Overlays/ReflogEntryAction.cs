using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>An action row of the reflog list: the key, what it does and the action a click on the row runs.</summary>
/// <param name="Key">The key shown on the left.</param>
/// <param name="Label">What the action does, shown on the right.</param>
/// <param name="Action">The action a click runs on the highlighted entry.</param>
public sealed record ReflogEntryAction(string Key, string Label, ActionId Action);
