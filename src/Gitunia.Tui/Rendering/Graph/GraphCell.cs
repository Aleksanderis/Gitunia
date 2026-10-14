namespace Gitunia.Tui.Rendering.Graph;

/// <summary>One character of the lane area and the lane it belongs to (for its colour); lane -1 is empty space.</summary>
public readonly record struct GraphCell(char Glyph, int Lane);
