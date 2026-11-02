using Gitunia.Core.Themes.Models;
using Terminal.Gui.Drawing;

namespace Gitunia.Tui.Views.Theme;

// Colour maths: contrast, blending and readable foregrounds.
public sealed partial class PaletteTable
{
    /// <summary>
    /// The foreground moved towards white (on a dark background) or black (on a light one) until it reaches
    /// <paramref name="minimum"/> contrast on <paramref name="background"/>; unchanged when it already does or when either
    /// colour is not a 24-bit one.
    /// </summary>
    public static Color EnsureReadable(Color foreground, Color background, double minimum)
    {
        if (foreground == Color.None || background == Color.None || ContrastOf(foreground, background) >= minimum)
        {
            return foreground;
        }

        var target = LuminanceOf(background) < LightnessMiddle ? Rgb(0xffffff) : Rgb(0x000000);
        for (var share = ReadableStep; share < 1.0; share += ReadableStep)
        {
            var candidate = Blend(foreground, target, share);
            if (ContrastOf(candidate, background) >= minimum)
            {
                return candidate;
            }
        }

        return target;
    }

    // A step from one colour towards another; the second one missing leaves the first.
    private static Color Blend(Color from, Color to, double share)
    {
        if (from == Color.None || to == Color.None)
        {
            return from;
        }

        return new Color(
            (int)Math.Round(from.R + ((to.R - from.R) * share)),
            (int)Math.Round(from.G + ((to.G - from.G) * share)),
            (int)Math.Round(from.B + ((to.B - from.B) * share)));
    }

    private static Color[] LabelForegrounds(ThemeDefinition definition) =>
        ReadableOn([.. definition.Lanes.Select(Rgb)], Role(definition, ThemeRoles.ChipForeground));

    private static Color Role(ThemeDefinition definition, string role) =>
        definition.Colors.TryGetValue(role, out var value) ? Rgb(value) : Color.None;

    // For every background the preferred foreground when readable (4.5) on it, else the better of black and white.
    private static Color[] ReadableOn(Color[] backgrounds, Color preferred) =>
        [.. backgrounds.Select(background => ReadableOn(background, preferred))];

    private static Color ReadableOn(Color background, Color preferred)
    {
        if (preferred != Color.None && ContrastOf(preferred, background) >= ReadableContrast)
        {
            return preferred;
        }

        var black = Rgb(0x000000);
        var white = Rgb(0xffffff);

        return ContrastOf(black, background) >= ContrastOf(white, background) ? black : white;
    }

    private static double ContrastOf(Color first, Color second)
    {
        var lighter = Math.Max(LuminanceOf(first), LuminanceOf(second));
        var darker = Math.Min(LuminanceOf(first), LuminanceOf(second));

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double LuminanceOf(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(int channel)
    {
        var value = channel / 255.0;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
