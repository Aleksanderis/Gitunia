using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Operations;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// Draws the in-progress operation banner (ux-workflows.md, workflow 4) on the bottom edge of the frame, in the status bar's
/// colours, its title a warning chip aligned with the mode badge. Where the banner does not fit (a terminal under
/// <see cref="ScreenLayout.MinimumHeightWithBanner"/> rows) the status bar carries its text instead. The same line is
/// used by the terminal view and the text frame, and its chips are the click targets. The editor hint comes last and
/// is left out when the chips would not leave room for it.
/// </summary>
public static class BannerRenderer
{
    private const string Gap = "  ";

    /// <summary>The banner as one line of <paramref name="width"/> columns plus the columns each chip
    /// occupies.</summary>
    public static BannerLine Build(OperationBanner banner, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        builder.Append($" {banner.Title} ", SegmentRole.StatusOperation);
        if (banner.Detail.Length > 0)
        {
            builder.Append($"{Gap}{banner.Detail}", SegmentRole.Status);
        }

        var areas = new List<BannerChipArea>(banner.Chips.Length);
        foreach (var chip in banner.Chips)
        {
            builder.Append(Gap, SegmentRole.Status);
            var start = width - builder.Remaining;
            HintBar.AppendOne(builder, new HintItem(chip.Keys, chip.Label));
            var end = width - builder.Remaining;
            if (end > start)
            {
                areas.Add(new BannerChipArea(start, end - start, ViewOf(chip.Action)));
            }
        }

        if (banner.Hint.Length > 0 && builder.Remaining >= Gap.Length + TextWidth.Of(banner.Hint))
        {
            builder.Append($"{Gap}{banner.Hint}", SegmentRole.Status);
        }

        return new BannerLine(builder.Build(SegmentRole.Status), areas);
    }

    /// <summary>The line to draw on the frame's bottom edge row: the banner where it applies, else <paramref
    /// name="edge"/>.</summary>
    /// <param name="model">The chrome content.</param>
    /// <param name="edge">The frame's own bottom edge line.</param>
    /// <param name="width">Terminal columns.</param>
    /// <param name="height">Terminal rows.</param>
    /// <param name="glyphs">The character set.</param>
    public static StyledLine EdgeLine(
        ScreenModel model,
        StyledLine edge,
        int width,
        int height,
        GraphGlyphSet glyphs)
    {
        if (model.Banner is { } banner && height >= ScreenLayout.MinimumHeightWithBanner)
        {
            return Build(banner, width, glyphs).Line;
        }

        return edge;
    }

    /// <summary>The model for the status bar: on a terminal too short for the banner row its text stands in for an
    /// empty message.</summary>
    public static ScreenModel ForStatusBar(ScreenModel model, int height)
    {
        if (model.Banner is { } banner && height < ScreenLayout.MinimumHeightWithBanner && model.Message.Length == 0)
        {
            return model with { Message = banner.PlainText };
        }

        return model;
    }

    /// <summary>The mouse view of the chip that runs <paramref name="action"/>.</summary>
    public static string ViewOf(ActionId action)
    {
        if (action == ActionIds.OperationContinue)
        {
            return DefaultMouseMap.BannerContinueButton;
        }

        if (action == ActionIds.OperationAbort)
        {
            return DefaultMouseMap.BannerAbortButton;
        }

        return DefaultMouseMap.BannerSkipButton;
    }
}
