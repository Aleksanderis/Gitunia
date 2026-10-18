namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>One chip of the tab row: its padded text and the mouse view a click on it is bound under.</summary>
/// <param name="Label">The text with one padding column on each side.</param>
/// <param name="View">The view name in the mouse map.</param>
public sealed record RemoteChip(string Label, string View);
