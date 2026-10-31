using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;
using Gitunia.Core.Themes.Models;
using Gitunia.Platform;

namespace Gitunia.Tui.Views.Theme;

/// <summary>Which colour scheme the <see cref="Palette"/> uses: the terminal's colour depth and whether its background is light.</summary>
/// <param name="Depth">
/// <see cref="ColorDepth.TrueColor"/> and <see cref="ColorDepth.Ansi256"/> use the 24-bit scheme (Terminal.Gui has no 256-colour output of its own),
/// <see cref="ColorDepth.Ansi16"/> only the 16 named colours (the terminal's own theme decides what they look like), <see cref="ColorDepth.None"/> text styles only.
/// </param>
/// <param name="IsLightBackground">The terminal has a light background (darker accents, light selection tints).</param>
/// <param name="PaintsBase">The scheme paints its own base text and background colours because the theme was forced against the terminal's background.</param>
/// <param name="Definition">
/// A theme file to take the colours (24-bit) and the 16-colour overrides from, null for the built-in schemes.
/// </param>
/// <param name="IsClassicConsole">The classic Windows console: the 16-colour scheme avoids dark blue, which is hard to read there (#574).</param>
public sealed record PaletteSettings(
    ColorDepth Depth,
    bool IsLightBackground,
    bool PaintsBase = false,
    ThemeDefinition? Definition = null,
    bool IsClassicConsole = false)
{
    /// <summary>The scheme used before the terminal is known: 24-bit colours for a dark background.</summary>
    public static PaletteSettings Default { get; } = new(ColorDepth.TrueColor, false);

    /// <summary>
    /// The same scheme with the light or dark side forced by the <c>theme</c> setting, or a theme file applied;
    /// <see cref="ThemeChoice.Auto"/> keeps the detected one, and so does a theme that cannot be loaded.
    /// </summary>
    /// <param name="theme">The configured choice.</param>
    /// <param name="themes">Where a named theme is looked up.</param>
    public PaletteSettings WithTheme(ThemeChoice theme, ThemeCatalog themes)
    {
        if (theme.IsNamedTheme)
        {
            if (themes.Find(theme.Name, []) is not { } definition)
            {
                return this;
            }

            return this with
            {
                IsLightBackground = definition.IsLight,
                PaintsBase = IsLightBackground != definition.IsLight,
                Definition = definition,
            };
        }

        return theme.Name switch
        {
            ThemeChoice.DarkName => this with { IsLightBackground = false, PaintsBase = IsLightBackground },
            ThemeChoice.LightName => this with { IsLightBackground = true, PaintsBase = !IsLightBackground },
            _ => this,
        };
    }

    /// <summary>The scheme for a terminal. The driver's own detection can only lower the depth: a driver that cannot do 24-bit colour gets the 16-colour scheme.</summary>
    /// <param name="capabilities">What the platform detected.</param>
    /// <param name="driverEmitsTrueColor">The Terminal.Gui driver writes 24-bit colour sequences.</param>
    public static PaletteSettings Choose(ConsoleCapabilities capabilities, bool driverEmitsTrueColor)
    {
        var depth = capabilities.Color is ColorDepth.TrueColor or ColorDepth.Ansi256 && !driverEmitsTrueColor ? ColorDepth.Ansi16 : capabilities.Color;
        return new PaletteSettings(depth, capabilities.IsLightBackground, IsClassicConsole: capabilities.IsClassicConsole);
    }
}
