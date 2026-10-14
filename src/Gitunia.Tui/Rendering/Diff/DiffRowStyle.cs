using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The marker character and color role of a changed diff line.</summary>
/// <param name="Marker">The character drawn in the marker column.</param>
/// <param name="Role">The color role of the line.</param>
public readonly record struct DiffRowStyle(char Marker, SegmentRole Role);
