using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Operations;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// Draws the in-progress operation banner (ux.md, workflow 4) on the bottom edge of the frame, in the status bar's
/// colours so it
/// reads as a bar and needs no colour of its own. Where the banner does not fit (a terminal under
/// <see cref="ScreenLayout.MinimumHeightWithBanner"/> rows) the status bar carries its text instead. The same line is
/// used by the
/// terminal view and the text frame, and its chips are the click targets.
/// </summary>
public static class BannerRenderer
{
    private const string TitlePadding = " ";
    private const string Gap = "  ";

    /// <summary>The banner as one line of <paramref name="width"/> columns plus the columns each chip
    /// occupies.</summary>
    public static BannerLine Build(OperationBanner banner, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(TitlePadding, SegmentRole.Status);
        builder.Append($" {banner.Title} ", SegmentRole.StatusMode);
        if (banner.Detail.Length > 0)
        {
            builder.Append($"{Gap}{banner.Detail}", SegmentRole.Status);
        }

        var areas = new List<BannerChipArea>(banner.Chips.Length);
        foreach (var chip in banner.Chips)
        {
            builder.Append(Gap, SegmentRole.Status);
            var start = width - builder.Remaining;
            builder.Append(chip.Keys, SegmentRole.StatusKey);
            builder.Append($" {chip.Label}", SegmentRole.Status);
            var end = width - builder.Remaining;
            if (end > start)
            {
                areas.Add(new BannerChipArea(start, end - start, ViewOf(chip.Action)));
            }
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
        GraphGlyphSet glyphs) =>
        model.Banner is { } banner && height >= ScreenLayout.MinimumHeightWithBanner
            ? Build(banner, width, glyphs).Line
            : edge;

    /// <summary>The model for the status bar: on a terminal too short for the banner row its text stands in for an
    /// empty message.</summary>
    public static ScreenModel ForStatusBar(ScreenModel model, int height) =>
        model.Banner is { } banner && height < ScreenLayout.MinimumHeightWithBanner && model.Message.Length == 0
            ? model with { Message = banner.PlainText }
            : model;

    /// <summary>The mouse view of the chip that runs <paramref name="action"/>.</summary>
    public static string ViewOf(ActionId action)
    {
        if (action == ActionIds.OperationContinue)
        {
            return DefaultMouseMap.BannerContinueButton;
        }

        return action == ActionIds.OperationAbort
            ? DefaultMouseMap.BannerAbortButton
            : DefaultMouseMap.BannerSkipButton;
    }
}
