namespace Gitunia.Tui.Views.Theme;

/// <summary>Whether a segment is drawn as selected and with the focused tint.</summary>
/// <param name="IsSelected">The segment is on a selected row.</param>
/// <param name="IsFocused">The pane holding the row has focus.</param>
/// <param name="IsRange">The row is in a Visual range: drawn stronger than the selected row.</param>
/// <param name="IsRangeCursor">The row is the moving end of the Visual range (#823): the range look plus <see cref="PaletteTable.RangeCursorStyle"/>.</param>
public readonly record struct SegmentHighlight(bool IsSelected, bool IsFocused, bool IsRange = false, bool IsRangeCursor = false);
