namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>One line of the settings list: a row, or a category heading of the collapsed layout.</summary>
/// <param name="RowIndex">The index into the rows of the state; -1 for a heading.</param>
/// <param name="Heading">The heading text, empty for a row.</param>
public sealed record SettingsBodyEntry(int RowIndex, string Heading);
