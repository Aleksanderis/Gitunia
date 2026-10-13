using Gitunia.Platform;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;
using Terminal.Gui.Drawing;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;
using StyledSegment = Gitunia.Tui.Rendering.StyledSegment;

namespace Gitunia.Tui.Tests.Views;

public class PaletteTests
{
    // What a terminal typically draws behind and in front of default text.
    private const int DarkBackground = 0x1e1e2e;
    private const int DarkText = 0xcdd6f4;
    private const int LightBackground = 0xeff1f5;
    private const int LightText = 0x4c4f69;

    private const double ReadableRatio = 4.5;
    private const double DecorationRatio = 3.0;

    public static TheoryData<bool> Variants => new() { false, true };

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenColouringAnyRole_ThenTextIsReadableOnItsBackground(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));
        var terminalBackground = isLight ? LightBackground : DarkBackground;
        var terminalText = isLight ? LightText : DarkText;

        var failures = new List<string>();
        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            foreach (var (isSelected, isFocused) in new[] { (false, false), (true, true), (true, false) })
            {
                var attribute = Palette.For(new StyledSegment("x", role), isSelected, isFocused, colors);
                var background = attribute.Background == Color.None ? terminalBackground : ToRgb(attribute.Background);
                var foreground = attribute.Foreground == Color.None ? terminalText : ToRgb(attribute.Foreground);
                var minimum = role is SegmentRole.Border ? DecorationRatio : ReadableRatio;
                if (role is SegmentRole.Border && isSelected)
                {
                    // A divider is never on a selected row.
                    continue;
                }

                var ratio = Contrast(foreground, background);

                if (ratio < minimum)
                {
                    failures.Add($"{role} (selected={isSelected}, focused={isFocused}): contrast {ratio:F2} is below {minimum}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenColouringLanes_ThenEveryLaneIsReadableOnTheTerminalBackground(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));

        for (var lane = 0; lane < colors.Lanes.Length; lane++)
        {
            var attribute = Palette.For(new StyledSegment("x", SegmentRole.Lane, lane), false, false, colors);

            Assert.True(Contrast(ToRgb(attribute.Foreground), isLight ? LightBackground : DarkBackground) >= ReadableRatio, $"lane {lane}");
        }
    }

    [Theory]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.TrueColor, true)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    public void GivenAColourScheme_WhenColouringRefs_ThenRemoteBranchesLocalBranchesTagsAndTheCurrentBranchAllDiffer(ColorDepth depth, bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));

        var foregrounds = new[] { SegmentRole.RemoteBranch, SegmentRole.LocalBranch, SegmentRole.Tag, SegmentRole.CurrentBranch }
            .Select(role => Palette.For(new StyledSegment("x", role), false, false, colors).Foreground)
            .ToArray();

        Assert.Equal(4, foregrounds.Distinct().Count());
    }

    [Theory]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.TrueColor, true)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    public void GivenAColourScheme_WhenSelectingARow_ThenNoRoleUsesInverseVideo(ColorDepth depth, bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));

        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            foreach (var isSelected in new[] { false, true })
            {
                var style = Palette.For(new StyledSegment("x", role), isSelected, true, colors).Style;

                Assert.False(style.HasFlag(TextStyle.Reverse), $"{role} (selected={isSelected}) is drawn in inverse video");
            }
        }
    }

    [Theory]
    [InlineData(SegmentRole.LocalBranch)]
    [InlineData(SegmentRole.RemoteBranch)]
    [InlineData(SegmentRole.Tag)]
    [InlineData(SegmentRole.DiffAdded)]
    [InlineData(SegmentRole.Lane)]
    public void GivenTrueColorScheme_WhenSelectingARow_ThenTheTextKeepsItsOwnColour(SegmentRole role)
    {
        var colors = PaletteTable.For(PaletteSettings.Default);
        var segment = new StyledSegment("x", role, 2);

        var plain = Palette.For(segment, false, true, colors);
        var selected = Palette.For(segment, true, true, colors);

        Assert.Equal(plain.Foreground, selected.Foreground);
        Assert.NotEqual(plain.Background, selected.Background);
    }

    [Theory]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.TrueColor, true)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    public void GivenAColourScheme_WhenColouringRemoteBranches_ThenTheyDoNotUseTheColourOfErrorsAndDeletions(ColorDepth depth, bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));

        var remote = Palette.For(new StyledSegment("origin/main", SegmentRole.RemoteBranch), false, false, colors).Foreground;

        Assert.NotEqual(Palette.For(new StyledSegment("x", SegmentRole.Error), false, false, colors).Foreground, remote);
        Assert.NotEqual(Palette.For(new StyledSegment("x", SegmentRole.DiffRemoved), false, false, colors).Foreground, remote);
        Assert.NotEqual(Palette.For(new StyledSegment("x", SegmentRole.FileDeleted), false, false, colors).Foreground, remote);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenSelectingInAPaneWithoutFocus_ThenTheTintIsDifferentFromTheFocusedOne(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));
        var segment = new StyledSegment("x", SegmentRole.Plain);

        var focused = Palette.For(segment, true, true, colors);
        var dim = Palette.For(segment, true, false, colors);
        var unselected = Palette.For(segment, false, false, colors);

        Assert.NotEqual(focused.Background, dim.Background);
        Assert.NotEqual(unselected.Background, dim.Background);
    }

    [Fact]
    public void GivenSixteenColourScheme_WhenSelectingWithoutFocus_ThenTheRowIsStillMarked()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false));
        var segment = new StyledSegment("x", SegmentRole.Plain);

        var dim = Palette.For(segment, true, false, colors);
        var focused = Palette.For(segment, true, true, colors);

        Assert.True(dim.Style.HasFlag(TextStyle.Underline));
        Assert.NotEqual(Color.None, focused.Background);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenSixteenColourScheme_WhenColouringAnyRole_ThenOnlyTheSixteenNamedColoursAreUsed(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, isLight));
        var named = Enum.GetValues<ColorName16>().Select(name => new Color(name)).ToHashSet();

        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            foreach (var (isSelected, isFocused) in new[] { (false, false), (true, true), (true, false) })
            {
                var attribute = Palette.For(new StyledSegment("x", role, 1), isSelected, isFocused, colors);

                Assert.True(IsNamedOrDefault(attribute.Foreground, named), $"{role} foreground");
                Assert.True(IsNamedOrDefault(attribute.Background, named), $"{role} background");
                if (attribute.Background != Color.None)
                {
                    Assert.NotEqual(attribute.Foreground, attribute.Background);
                }
            }
        }
    }

    [Fact]
    public void GivenSixteenColourDarkScheme_WhenSelectingMutedText_ThenItIsLighterThanTheSelectionBackground()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false));

        var attribute = Palette.For(new StyledSegment("x", SegmentRole.Hash), true, true, colors);

        Assert.NotEqual(attribute.Foreground, attribute.Background);
    }

    [Fact]
    public void GivenNoColour_WhenSelectingARow_ThenOnlyTextStylesMarkIt()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.None, false));

        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            var attribute = Palette.For(new StyledSegment("x", role), true, true, colors);

            Assert.Equal(Color.None, attribute.Foreground);
            Assert.Equal(Color.None, attribute.Background);
            Assert.True(attribute.Style.HasFlag(TextStyle.Reverse));
        }
    }

    [Theory]
    [InlineData(ColorDepth.None, false)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    [InlineData(ColorDepth.TrueColor, false)]
    public void GivenAnyScheme_WhenColouringRolesThatCarryMeaning_ThenTheyLookDifferentFromPlainText(ColorDepth depth, bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));
        var plain = Palette.For(new StyledSegment("x", SegmentRole.Plain), false, false, colors);

        foreach (var role in new[] { SegmentRole.SearchMatch, SegmentRole.ActiveTab, SegmentRole.InactiveTab, SegmentRole.FocusedBorder, SegmentRole.CurrentBranch, SegmentRole.StatusMode })
        {
            Assert.NotEqual(plain, Palette.For(new StyledSegment("x", role), false, false, colors));
        }

        Assert.NotEqual(
            Palette.For(new StyledSegment("x", SegmentRole.ActiveTab), false, false, colors),
            Palette.For(new StyledSegment("x", SegmentRole.InactiveTab), false, false, colors));
        Assert.NotEqual(
            Palette.For(new StyledSegment("x", SegmentRole.LocalBranch), false, false, colors),
            Palette.For(new StyledSegment("x", SegmentRole.CurrentBranch), false, false, colors));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenColouringLanesOnASelectedRow_ThenEveryLaneIsReadable(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));

        for (var lane = 0; lane < colors.Lanes.Length; lane++)
        {
            foreach (var isFocused in new[] { true, false })
            {
                var attribute = Palette.For(new StyledSegment("x", SegmentRole.Lane, lane), true, isFocused, colors);

                Assert.True(Contrast(ToRgb(attribute.Foreground), ToRgb(attribute.Background)) >= ReadableRatio, $"lane {lane}, focused {isFocused}");
            }
        }
    }

    [Fact]
    public void GivenMoreLanesThanColours_WhenColouring_ThenTheColoursCycle()
    {
        var colors = PaletteTable.For(PaletteSettings.Default);
        var first = Palette.For(new StyledSegment("x", SegmentRole.Lane, 0), false, false, colors);

        var wrapped = Palette.For(new StyledSegment("x", SegmentRole.Lane, colors.Lanes.Length), false, false, colors);

        Assert.Equal(first.Foreground, wrapped.Foreground);
    }

    [Fact]
    public void GivenTheActiveTabAndTheFocusedBorder_WhenColouring_ThenBothUseTheAccent()
    {
        var colors = PaletteTable.For(PaletteSettings.Default);

        var tab = Palette.For(new StyledSegment("x", SegmentRole.ActiveTab), false, false, colors);
        var border = Palette.For(new StyledSegment("x", SegmentRole.FocusedBorder), false, false, colors);
        var quiet = Palette.For(new StyledSegment("x", SegmentRole.Border), false, false, colors);

        Assert.Equal(colors.Accent, tab.Background);
        Assert.Equal(colors.Accent, border.Foreground);
        Assert.NotEqual(colors.Accent, quiet.Foreground);
    }

    [Theory]
    [InlineData(ColorDepth.TrueColor, true, ColorDepth.TrueColor)]
    [InlineData(ColorDepth.TrueColor, false, ColorDepth.Ansi16)]
    [InlineData(ColorDepth.Ansi256, false, ColorDepth.Ansi16)]
    [InlineData(ColorDepth.Ansi256, true, ColorDepth.Ansi256)]
    [InlineData(ColorDepth.Ansi16, true, ColorDepth.Ansi16)]
    [InlineData(ColorDepth.None, true, ColorDepth.None)]
    [InlineData(ColorDepth.None, false, ColorDepth.None)]
    public void GivenDetectedDepthAndDriver_WhenChoosingTheScheme_ThenTheDriverCanOnlyLowerIt(ColorDepth detected, bool driverEmitsTrueColor, ColorDepth expected)
    {
        var settings = PaletteSettings.Choose(new ConsoleCapabilities(true, detected, true, true), driverEmitsTrueColor);

        Assert.Equal(expected, settings.Depth);
        Assert.True(settings.IsLightBackground);
    }

    private static bool IsNamedOrDefault(Color color, HashSet<Color> named) => color == Color.None || named.Contains(color);

    private static int ToRgb(Color color) => (color.R << 16) | (color.G << 8) | color.B;

    private static double Contrast(int first, int second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(int rgb) =>
        (0.2126 * Linear((rgb >> 16) & 0xff)) + (0.7152 * Linear((rgb >> 8) & 0xff)) + (0.0722 * Linear(rgb & 0xff));

    private static double Linear(int channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
