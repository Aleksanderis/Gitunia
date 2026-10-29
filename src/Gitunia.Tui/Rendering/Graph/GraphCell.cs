namespace Gitunia.Tui.Rendering.Graph;

/// <summary>One character of the lane area and its colour slot (see BranchColors); -1 is empty space.</summary>
public readonly record struct GraphCell(char Glyph, int Color);
