namespace Gitunia.Tui.Rendering;

/// <summary>Which sides of a graph cell a line leaves through; the combination picks the box-drawing character.</summary>
[Flags]
public enum LineDirections
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,
}
