namespace Gitunia.Tui.Rendering.Graph;

/// <summary>One cell of the lane area: a glyph (a string, as some line characters are outside the Basic Multilingual Plane) and its colour slot (see BranchColors); -1 is empty space.</summary>
public readonly record struct GraphCell(string Glyph, int Color);
