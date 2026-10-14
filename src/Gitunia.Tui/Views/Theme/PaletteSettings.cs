using Gitunia.Platform;

namespace Gitunia.Tui.Views.Theme;

/// <summary>Which colour scheme the <see cref="Palette"/> uses: the terminal's colour depth and whether its background is light.</summary>
/// <param name="Depth">
/// <see cref="ColorDepth.TrueColor"/> and <see cref="ColorDepth.Ansi256"/> use the 24-bit scheme (Terminal.Gui has no 256-colour output of its own),
/// <see cref="ColorDepth.Ansi16"/> only the 16 named colours (the terminal's own theme decides what they look like), <see cref="ColorDepth.None"/> text styles only.
/// </param>
/// <param name="IsLightBackground">The terminal has a light background (darker accents, light selection tints).</param>
public sealed record PaletteSettings(ColorDepth Depth, bool IsLightBackground)
{
    /// <summary>The scheme used before the terminal is known: 24-bit colours for a dark background.</summary>
    public static PaletteSettings Default { get; } = new(ColorDepth.TrueColor, false);

    /// <summary>The scheme for a terminal. The driver's own detection can only lower the depth: a driver that cannot do 24-bit colour gets the 16-colour scheme.</summary>
    /// <param name="capabilities">What the platform detected.</param>
    /// <param name="driverEmitsTrueColor">The Terminal.Gui driver writes 24-bit colour sequences.</param>
    public static PaletteSettings Choose(ConsoleCapabilities capabilities, bool driverEmitsTrueColor)
    {
        var depth = capabilities.Color is ColorDepth.TrueColor or ColorDepth.Ansi256 && !driverEmitsTrueColor ? ColorDepth.Ansi16 : capabilities.Color;
        return new PaletteSettings(depth, capabilities.IsLightBackground);
    }
}
