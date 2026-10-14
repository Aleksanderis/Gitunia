namespace Gitunia.Tui;

/// <summary>The characters used to draw a box.</summary>
/// <param name="TopLeft">The top left corner.</param>
/// <param name="TopRight">The top right corner.</param>
/// <param name="BottomLeft">The bottom left corner.</param>
/// <param name="BottomRight">The bottom right corner.</param>
/// <param name="Vertical">The vertical edge.</param>
public readonly record struct BoxGlyphs(char TopLeft, char TopRight, char BottomLeft, char BottomRight, char Vertical)
{
    /// <summary>Gets the box drawing characters for terminals with Unicode support.</summary>
    public static BoxGlyphs Unicode { get; } = new('┌', '┐', '└', '┘', '│');

    /// <summary>Gets the plain ASCII fallback.</summary>
    public static BoxGlyphs Ascii { get; } = new('+', '+', '+', '+', '|');
}
