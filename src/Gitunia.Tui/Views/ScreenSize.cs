namespace Gitunia.Tui.Views;

/// <summary>The terminal size in character cells.</summary>
/// <param name="Width">Columns.</param>
/// <param name="Height">Rows.</param>
public readonly record struct ScreenSize(int Width, int Height);
