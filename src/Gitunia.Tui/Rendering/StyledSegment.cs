namespace Gitunia.Tui.Rendering;

/// <summary>A run of text with one meaning.</summary>
/// <param name="Text">The text; never wider than the line it belongs to.</param>
/// <param name="Role">What the text is.</param>
/// <param name="ColorIndex">Palette index for roles that have several colours (the lane of a graph cell).</param>
/// <param name="OnPanel">The text sits on the raised panel background of the bottom pane (see <see cref="Views.PaletteTable.PanelBackground"/>); a selected row keeps its selection tint.</param>
/// <param name="Selection">Only set by <see cref="ScreenComposer"/> where one line holds several panes; the views leave it to the line.</param>
public readonly record struct StyledSegment(string Text, SegmentRole Role, int ColorIndex = 0, SegmentSelection Selection = SegmentSelection.Inherit, bool OnPanel = false);
