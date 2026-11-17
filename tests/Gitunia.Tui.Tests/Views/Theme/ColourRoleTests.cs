using Gitunia.Platform;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Drawing;
using Xunit;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Tests.Views.Theme;

/// <summary>One role per meaning (#1449): key labels, 16-colour slots and the NO_COLOR styles.</summary>
public class ColourRoleTests
{
    public static TheoryData<ColorDepth, bool, bool> Tiers =>
        new()
        {
            { ColorDepth.TrueColor, false, false },
            { ColorDepth.TrueColor, true, false },
            { ColorDepth.Ansi16, false, false },
            { ColorDepth.Ansi16, true, false },
            { ColorDepth.Ansi16, false, true },
            { ColorDepth.None, false, false },
        };

    [Theory]
    [MemberData(nameof(Tiers))]
    public void GivenAnyTier_WhenDrawingKeyLabels_ThenTheyAreTheAccentInBold(ColorDepth depth, bool isLight, bool isClassic)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight, IsClassicConsole: isClassic));

        var body = Palette.For(new StyledSegment("Enter", SegmentRole.KeyLabel), false, false, colors);
        var bar = Palette.For(new StyledSegment("Enter", SegmentRole.BarKeyLabel), false, false, colors);

        Assert.Equal(colors.Accent, body.Foreground);
        Assert.Equal(colors.Accent, bar.Foreground);
        Assert.True(body.Style.HasFlag(TextStyle.Bold));
        Assert.True(bar.Style.HasFlag(TextStyle.Bold));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GivenTheSixteenColourTiers_WhenColouringMeanings_ThenNoTwoShareASlot(bool isLight, bool isClassic)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, isLight, IsClassicConsole: isClassic));

        Color[] meanings = [colors.Accent, colors.Good, colors.Bad, colors.Remote, colors.Warn, colors.Info];

        Assert.Equal(meanings.Length, meanings.Distinct().Count());
        Assert.NotEqual(colors.SelectionFocused, colors.MutedOnSelection);
    }

    [Fact]
    public void GivenNoColour_WhenDrawingSeveritiesAndSelections_ThenEachHasItsOwnStyle()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.None, false));
        SegmentRole[] roles =
        [
            SegmentRole.StatusNotice,
            SegmentRole.StatusProgress,
            SegmentRole.StatusWarning,
            SegmentRole.StatusError,
            SegmentRole.StatusNoticeFresh,
            SegmentRole.StatusWarningFresh,
            SegmentRole.StatusErrorFresh,
        ];

        var styles = roles.Select(role => Palette.For(new StyledSegment("x", role), false, false, colors).Style).ToList();

        Assert.Equal(styles.Count, styles.Distinct().Count());
        var error = Palette.For(new StyledSegment("x", SegmentRole.Error), false, false, colors).Style;
        var warning = Palette.For(new StyledSegment("x", SegmentRole.Warning), false, false, colors).Style;
        Assert.NotEqual(warning, error);
        Assert.True(error.HasFlag(TextStyle.Underline));
        var unfocused = Palette.For(new StyledSegment("x", SegmentRole.Plain), true, false, colors).Style;
        var button = Palette.For(new StyledSegment("x", SegmentRole.RepositoryTab), false, false, colors).Style;
        var title = Palette.For(new StyledSegment("x", SegmentRole.SelectedTab), false, false, colors).Style;
        Assert.Equal(3, new[] { unfocused, button, title }.Distinct().Count());
    }

    [Fact]
    public void GivenTrueColour_WhenDrawingADirectoryName_ThenItIsNotTheHeadingLook()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, false));

        var directory = Palette.For(new StyledSegment("src/", SegmentRole.DirectoryName), false, false, colors);
        var heading = Palette.For(new StyledSegment("src/", SegmentRole.Heading), false, false, colors);

        Assert.NotEqual(heading, directory);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void GivenAnyTier_WhenDrawingADivider_ThenItUsesTheBorderColour(ColorDepth depth, bool isLight, bool isClassic)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight, IsClassicConsole: isClassic));

        var divider = Palette.For(new StyledSegment("|", SegmentRole.Divider), false, false, colors);
        var border = Palette.For(new StyledSegment("|", SegmentRole.Border), false, false, colors);

        Assert.Equal(border, divider);
    }
}
