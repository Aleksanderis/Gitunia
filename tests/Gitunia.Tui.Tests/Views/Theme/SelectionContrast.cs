using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Drawing;

namespace Gitunia.Tui.Tests.Views.Theme;

/// <summary>
/// The contrast a role must reach on a selected row. The focused bar is deliberately a visible step from the page
/// (about 1.8:1 in dark, about 1.4:1 in light, #934), which leaves accent coloured text below the 4.5 body ratio, so
/// accent colours (refs, lanes, errors, links) need the lower UI-component ratio there while text, muted and subtle
/// text keep the full one.
/// </summary>
internal static class SelectionContrast
{
    public const double AccentOnSelectionRatio = 3.5;

    public static double Minimum(double ordinary, bool isSelected, int foreground, PaletteTable colors) =>
        isSelected && IsAccent(foreground, colors) ? Math.Min(ordinary, AccentOnSelectionRatio) : ordinary;

    private static bool IsAccent(int foreground, PaletteTable colors)
    {
        Color[] accents =
            [colors.Accent, colors.Good, colors.Bad, colors.Remote, colors.Warn, colors.Info, .. colors.Lanes];

        return accents.Any(accent => (accent.R << 16 | accent.G << 8 | accent.B) == foreground);
    }
}
