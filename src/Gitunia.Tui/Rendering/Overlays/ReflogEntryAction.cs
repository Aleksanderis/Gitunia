namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>An action row of the reflog list: the key, what it does and the action a click on the row runs.</summary>
/// <param name="Key">The key shown on the left.</param>
/// <param name="Label">What the action does, shown on the right.</param>
/// <param name="View">The mouse view of the row (see <c>DefaultMouseMap</c>); the map names its action.</param>
public sealed record ReflogEntryAction(string Key, string Label, string View);
