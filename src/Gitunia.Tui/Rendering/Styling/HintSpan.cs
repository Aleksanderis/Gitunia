namespace Gitunia.Tui.Rendering.Styling;

/// <summary>The columns one hint took in a row; a hint that was cut off completely has width 0.</summary>
/// <param name="Start">The first column, counted from the left edge of the line builder.</param>
/// <param name="Width">How many columns it took.</param>
public readonly record struct HintSpan(int Start, int Width);
