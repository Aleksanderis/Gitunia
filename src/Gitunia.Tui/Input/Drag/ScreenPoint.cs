namespace Gitunia.Tui.Input.Drag;

/// <summary>A terminal cell.</summary>
/// <param name="X">Zero-based column.</param>
/// <param name="Y">Zero-based row.</param>
public readonly record struct ScreenPoint(int X, int Y);
