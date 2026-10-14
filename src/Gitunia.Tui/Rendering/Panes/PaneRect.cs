namespace Gitunia.Tui.Rendering.Panes;

/// <summary>A rectangle of the screen in columns and rows; a pane that is not shown has no width or no height.</summary>
public readonly record struct PaneRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
