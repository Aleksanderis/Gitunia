using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>The marker and colour role of a diff row in an overlay.</summary>
/// <param name="Marker">The text before the row.</param>
/// <param name="Role">The colour role.</param>
public readonly record struct DiffMark(string Marker, SegmentRole Role);
