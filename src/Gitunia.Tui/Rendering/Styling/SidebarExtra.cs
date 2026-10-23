
namespace Gitunia.Tui.Rendering.Styling;

/// <summary>A piece of text drawn after a sidebar label (tracking counts, annotation).</summary>
/// <param name="Text">The text.</param>
/// <param name="Role">How it is coloured.</param>
public readonly record struct SidebarExtra(string Text, SegmentRole Role);
