using Gitunia.Tui.Rendering.Screen;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>A run of text with one meaning.</summary>
/// <param name="Text">The text; never wider than the line it belongs to.</param>
/// <param name="Role">What the text is.</param>
/// <param name="ColorIndex">Palette index for roles that have several colours (the lane of a graph cell).</param>
/// <param name="InGroup">The text is in the change group under the cursor of the working-tree diff: it gets the group tint where the row is not selected.</param>
/// <param name="Selection">Only set by <see cref="ScreenComposer"/> where one line holds several panes; the views leave it to the line.</param>
public readonly record struct StyledSegment(string Text, SegmentRole Role, int ColorIndex = 0, SegmentSelection Selection = SegmentSelection.Inherit, bool InGroup = false);
