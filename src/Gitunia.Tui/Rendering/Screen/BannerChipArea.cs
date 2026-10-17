namespace Gitunia.Tui.Rendering.Screen;

/// <summary>The columns of one banner chip and the mouse view a click on it goes to.</summary>
/// <param name="Start">First column.</param>
/// <param name="Width">Columns the chip takes.</param>
/// <param name="View">The mouse view (<c>BannerButton.*</c>).</param>
public sealed record BannerChipArea(int Start, int Width, string View);
