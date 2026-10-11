namespace Gitunia.Tui.Rendering;

/// <summary>A run of text with one meaning.</summary>
/// <param name="Text">The text; never wider than the line it belongs to.</param>
/// <param name="Role">What the text is.</param>
/// <param name="ColorIndex">Palette index for roles that have several colours (the lane of a graph cell).</param>
public readonly record struct StyledSegment(string Text, SegmentRole Role, int ColorIndex = 0);
