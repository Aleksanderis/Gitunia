namespace Gitunia.Tui.Rendering.Graph;

/// <summary>Which sides of a graph cell a line leaves through; the combination picks the box-drawing character.</summary>
[Flags]
public enum LineDirections
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,

    /// <summary>The cell holds a diagonal from its top left to its bottom right (a line stepping into the lane below on its right).</summary>
    Backslash = 16,

    /// <summary>The cell holds a diagonal from its bottom left to its top right.</summary>
    Slash = 32,

    /// <summary>The cell holds a lane passing through the row; a marker only, never a glyph of its own.</summary>
    Through = 64,
}
