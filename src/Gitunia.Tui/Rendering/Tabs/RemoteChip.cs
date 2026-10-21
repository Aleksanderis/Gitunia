namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>One chip of the tab row: its padded text and the mouse view a click on it is bound under.</summary>
/// <param name="Label">The text with a padding column on each side, the key text after the first if any.</param>
/// <param name="View">The view name in the mouse map.</param>
/// <param name="KeyLength">Length of the key text in <paramref name="Label"/>, 0 for none.</param>
public sealed record RemoteChip(string Label, string View, int KeyLength = 0);
