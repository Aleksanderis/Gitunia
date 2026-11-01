namespace Gitunia.Core.Shell;

/// <summary>The size of the terminal in cells, as far as logic needs it (whether a dialog fits).</summary>
/// <param name="Width">Columns.</param>
/// <param name="Height">Rows.</param>
public sealed record ScreenExtent(int Width, int Height);
