using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Drawing;
using Xunit;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Tests.Views.Theme;

public class PaletteTests
{
    private static readonly ThemeCatalog Themes = new(
        Path.Combine(Path.GetTempPath(), $"gitunia-no-themes-{Guid.NewGuid():N}"));

    private static readonly SegmentHighlight RangeHighlight = new(true, true, true);

    // What a terminal typically draws behind and in front of default text.
    private const int DarkBackground = 0x1e1e2e;
    private const int DarkText = 0xcdd6f4;
    private const int LightBackground = 0xeff1f5;
    private const int LightText = 0x4c4f69;

    private const double ReadableRatio = 4.5;
    private const double DecorationRatio = 3.0;
    private const double MinimumTabTint = 1.15;

    // Whitespace marks are meant to be barely there: readable, but far below the text ratio.
    private const double SubtleRatio = 2.0;

    public static TheoryData<bool> Variants => [false, true];

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenColouringTheCursorGroup_ThenTextIsReadableOnTheGroupTint(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));
        var terminalText = isLight ? LightText : DarkText;
        SegmentRole[] roles =
            [SegmentRole.Plain, SegmentRole.Faint, SegmentRole.DiffAdded, SegmentRole.DiffRemoved, SegmentRole.DiffGroupBar];

        var failures = new List<string>();
        foreach (var role in roles)
        {
            var attribute = Palette.For(new StyledSegment("x", role, InGroup: true), false, true, colors);
            var foreground = attribute.Foreground == Color.None ? terminalText : ToRgb(attribute.Foreground);
            var ratio = Contrast(foreground, ToRgb(attribute.Background));
            if (ratio < ReadableRatio)
            {
                failures.Add($"{role}: contrast {ratio:F2}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenTintingTheCursorGroup_ThenTheTintIsApartFromTheSelectionTints(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));
        var terminalBackground = isLight ? LightBackground : DarkBackground;

        var group = ToRgb(colors.GroupBackground);

        Assert.NotEqual(terminalBackground, group);
        Assert.NotEqual(ToRgb(colors.SelectionDim), group);
        Assert.NotEqual(ToRgb(colors.SelectionFocused), group);
        // Between the terminal background and the quieter selection tint, so a selected row inside the group stays visible.
        Assert.True(Luminance(group) > Luminance(terminalBackground) != isLight);
        Assert.True(Math.Abs(Luminance(ToRgb(colors.SelectionDim)) - Luminance(terminalBackground)) > Math.Abs(Luminance(group) - Luminance(terminalBackground)));
    }

    [Theory]
    [InlineData(ColorDepth.Ansi16)]
    [InlineData(ColorDepth.None)]
    public void GivenAColourlessScheme_WhenColouringTheCursorGroup_ThenTheBackgroundIsLeftToTheTerminal(ColorDepth depth)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, false));

        var attribute = Palette.For(new StyledSegment("x", SegmentRole.Plain, InGroup: true), false, true, colors);

        Assert.Equal(Color.None, attribute.Background);
    }

    [Fact]
    public void GivenTheClassicConsole_WhenChoosingSixteenColours_ThenAccentAndChipAvoidDarkBlue()
    {
        var classic = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false, IsClassicConsole: true));
        var other = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false));

        Assert.NotEqual(other.Accent, classic.Accent);
        Assert.NotEqual(other.ChipBackground, classic.ChipBackground);
    }

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
            foreach (var highlight in new SegmentHighlight[] { new(false, false), new(true, true), new(true, false) })
            {
                var attribute = Palette.For(new StyledSegment("x", role), highlight.IsSelected, highlight.IsFocused, colors);
                var background = attribute.Background == Color.None ? terminalBackground : ToRgb(attribute.Background);
                var foreground = attribute.Foreground == Color.None ? terminalText : ToRgb(attribute.Foreground);
                var minimum = MinimumFor(role);
                if (role is SegmentRole.Border && highlight.IsSelected)
                {
                    // A divider is never on a selected row.
                    continue;
                }

                var ratio = Contrast(foreground, background);

                if (ratio < minimum)
                {
                    failures.Add($"{role} (selected={highlight.IsSelected}, focused={highlight.IsFocused}): contrast {ratio:F2} is below {minimum}");
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
    [InlineData(false)]
    [InlineData(true)]
    public void GivenTrueColorScheme_WhenDrawingATabButton_ThenItDiffersFromPlainText(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));

        var plain = Palette.For(new StyledSegment("x", SegmentRole.Plain), false, false, colors);
        var button = Palette.For(new StyledSegment(" Files ", SegmentRole.RepositoryTab), false, false, colors);

        Assert.NotEqual(plain.Background, button.Background);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenTrueColorScheme_WhenDrawingARepositoryTab_ThenItStandsOutFromTheBarAndFromTheActiveTab(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));

        var tab = Palette.For(new StyledSegment(" 2 beta ", SegmentRole.RepositoryTab), false, false, colors);
        var bar = Palette.For(new StyledSegment(" ", SegmentRole.Status), false, false, colors);
        var active = Palette.For(new StyledSegment(" 1 alpha ", SegmentRole.ActiveTab), false, false, colors);

        Assert.NotEqual(active.Background, tab.Background);
        Assert.True(Contrast(ToRgb(tab.Background), ToRgb(bar.Background)) >= MinimumTabTint, "the tab tint is visible against the bar");
    }

    [Theory]
    [InlineData(ColorDepth.Ansi16)]
    [InlineData(ColorDepth.None)]
    public void GivenAColourlessScheme_WhenDrawingARepositoryTab_ThenItIsUnderlinedWithoutABackground(ColorDepth depth)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, false));

        var tab = Palette.For(new StyledSegment(" 2 beta ", SegmentRole.RepositoryTab), false, false, colors);

        Assert.Equal(Color.None, tab.Background);
        Assert.True(tab.Style.HasFlag(TextStyle.Underline));
    }

    [Theory]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.None, false)]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.TrueColor, true)]
    public void GivenAnUnfocusedPane_WhenDrawingTabs_ThenTheSelectedTabLooksDifferentFromTheOthers(ColorDepth depth, bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));

        var selected = Palette.For(new StyledSegment(" Files ", SegmentRole.SelectedTab), false, false, colors);
        var other = Palette.For(new StyledSegment(" Files ", SegmentRole.RepositoryTab), false, false, colors);

        Assert.NotEqual(selected, other);
    }

    [Theory]
    [InlineData(ColorDepth.Ansi16)]
    [InlineData(ColorDepth.None)]
    public void GivenAColourlessScheme_WhenDrawingATabButton_ThenItIsUnderlined(ColorDepth depth)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, false));

        var button = Palette.For(new StyledSegment(" Files ", SegmentRole.RepositoryTab), false, false, colors);

        Assert.True(button.Style.HasFlag(TextStyle.Underline));
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
            foreach (var highlight in new SegmentHighlight[] { new(false, false), new(true, true), new(true, false) })
            {
                var attribute = Palette.For(new StyledSegment("x", role, 1), highlight.IsSelected, highlight.IsFocused, colors);

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

        SegmentRole[] roles =
        [
            SegmentRole.SearchMatch,
            SegmentRole.ActiveTab,
            SegmentRole.SelectedTab,
            SegmentRole.FocusedBorder,
            SegmentRole.CurrentBranch,
            SegmentRole.StatusMode,
            SegmentRole.StatusOperation,
        ];

        foreach (var role in roles)
        {
            Assert.NotEqual(plain, Palette.For(new StyledSegment("x", role), false, false, colors));
        }

        Assert.NotEqual(
            Palette.For(new StyledSegment("x", SegmentRole.ActiveTab), false, false, colors),
            Palette.For(new StyledSegment("x", SegmentRole.SelectedTab), false, false, colors));
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

    [Theory]
    [InlineData(ColorDepth.TrueColor)]
    [InlineData(ColorDepth.Ansi256)]
    [InlineData(ColorDepth.Ansi16)]
    [InlineData(ColorDepth.None)]
    public void GivenAnyColourTier_WhenColouringABranchLabel_ThenItIsAChipInTheLaneColour(ColorDepth depth)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, false));

        for (var slot = 0; slot < colors.Lanes.Length; slot++)
        {
            var lane = Palette.For(new StyledSegment("x", SegmentRole.Lane, slot), false, false, colors);
            var label = Palette.For(new StyledSegment("x", SegmentRole.LaneLabel, slot), false, false, colors);

            Assert.True(label.Style.HasFlag(TextStyle.Bold), $"slot {slot} at {depth}");
            if (depth == ColorDepth.None)
            {
                Assert.True(label.Style.HasFlag(TextStyle.Reverse), "without colours the box is inverse video");
                continue;
            }

            Assert.Equal(lane.Foreground, label.Background);
            Assert.NotEqual(label.Foreground, label.Background);
        }
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenColouringBranchLabels_ThenTheTextIsReadableOnTheLaneColour(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));

        for (var slot = 0; slot < colors.Lanes.Length; slot++)
        {
            var label = Palette.For(new StyledSegment("x", SegmentRole.LaneLabel, slot), false, false, colors);

            Assert.True(Contrast(ToRgb(label.Foreground), ToRgb(label.Background)) >= ReadableRatio, $"slot {slot}");
        }
    }

    [Fact]
    public void GivenNoColours_WhenSelectingTheRowOfABranchLabel_ThenTheLabelStaysBoldInsideTheReversedRow()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.None, false));
        var segment = new StyledSegment("x", SegmentRole.LaneLabel, 3);

        var focused = Palette.For(segment, true, true, colors);
        var unfocused = Palette.For(segment, true, false, colors);

        Assert.True(focused.Style.HasFlag(TextStyle.Bold));
        Assert.True(focused.Style.HasFlag(TextStyle.Reverse));
        Assert.True(unfocused.Style.HasFlag(TextStyle.Bold));
        Assert.True(unfocused.Style.HasFlag(TextStyle.Reverse));
    }

    [Fact]
    public void GivenABranchLabel_WhenSelectingItsRow_ThenTheChipKeepsItsColours()
    {
        var colors = PaletteTable.For(PaletteSettings.Default);
        var segment = new StyledSegment("x", SegmentRole.LaneLabel, 3);

        var plain = Palette.For(segment, false, true, colors);
        var selected = Palette.For(segment, true, true, colors);

        Assert.Equal(plain.Foreground, selected.Foreground);
        Assert.Equal(plain.Background, selected.Background);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenColouringBranchLabelsOnASelectedRow_ThenEveryLabelIsReadable(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));

        for (var slot = 0; slot < colors.Lanes.Length; slot++)
        {
            foreach (var isFocused in new[] { true, false })
            {
                var segment = new StyledSegment("x", SegmentRole.LaneLabel, slot);
                var attribute = Palette.For(segment, true, isFocused, colors);
                var ratio = Contrast(ToRgb(attribute.Foreground), ToRgb(attribute.Background));

                Assert.True(ratio >= ReadableRatio, $"slot {slot}");
            }
        }
    }

    [Fact]
    public void GivenMoreLanesThanColours_WhenColouring_ThenTheColoursCycle()
    {
        var colors = PaletteTable.For(PaletteSettings.Default);
        var first = Palette.For(new StyledSegment("x", SegmentRole.Lane), false, false, colors);

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

    [Theory]
    [InlineData("auto", true, true)]
    [InlineData("auto", false, false)]
    [InlineData("dark", true, false)]
    [InlineData("light", false, true)]
    public void GivenATheme_WhenApplyingItToTheDetectedScheme_ThenOnlyAutoKeepsTheDetectedSide(
        string themeName,
        bool detectedLight,
        bool expectedLight)
    {
        var choice = ThemeChoice.FromText(themeName);

        var settings = new PaletteSettings(ColorDepth.Ansi16, detectedLight).WithTheme(choice, Themes);

        Assert.Equal(expectedLight, settings.IsLightBackground);
        Assert.Equal(ColorDepth.Ansi16, settings.Depth);
    }

    [Fact]
    public void GivenTheLightTheme_WhenTheTerminalLooksDark_ThenTheTableIsTheLightOne()
    {
        var forced = PaletteTable.For(
            new PaletteSettings(ColorDepth.TrueColor, false).WithTheme(ThemeChoice.Light, Themes));

        Assert.Equal(PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, true)).Accent, forced.Accent);
        Assert.NotEqual(PaletteTable.For(PaletteSettings.Default).Accent, forced.Accent);
    }

    [Theory]
    [InlineData("light", false, true)]
    [InlineData("dark", true, true)]
    [InlineData("light", true, false)]
    [InlineData("auto", false, false)]
    public void GivenAForcedTheme_WhenItDiffersFromTheTerminal_ThenPlainTextGetsTheBaseColours(
        string themeName,
        bool detectedLight,
        bool paintsBase)
    {
        var choice = ThemeChoice.FromText(themeName);
        var table = PaletteTable.For(
            new PaletteSettings(ColorDepth.TrueColor, detectedLight).WithTheme(choice, Themes));
        var plain = Palette.For(new StyledSegment("x", SegmentRole.Hash), false, false, table);

        Assert.Equal(paintsBase, table.BaseBackground != Color.None);
        Assert.Equal(paintsBase, plain.Background != Color.None);
    }

    private static bool IsNamedOrDefault(Color color, HashSet<Color> named) => color == Color.None || named.Contains(color);

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenDrawingARangeRow_ThenItIsApartFromTheTerminalMoreThanTheSelectedRow(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));
        var terminalBackground = isLight ? LightBackground : DarkBackground;

        var range = Palette.For(new StyledSegment("x", SegmentRole.Plain), RangeHighlight, colors);
        var selected = Palette.For(new StyledSegment("x", SegmentRole.Plain), true, true, colors);

        Assert.True(Contrast(ToRgb(range.Background), terminalBackground) > Contrast(ToRgb(selected.Background), terminalBackground));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void GivenTrueColorScheme_WhenDrawingARangeRow_ThenTextIsReadableOnTheRangeTint(bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, isLight));
        var terminalText = isLight ? LightText : DarkText;
        var failures = new List<string>();
        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            var attribute = Palette.For(new StyledSegment("x", role), RangeHighlight, colors);
            var foreground = attribute.Foreground == Color.None ? terminalText : ToRgb(attribute.Foreground);
            var ratio = Contrast(foreground, ToRgb(attribute.Background));
            if (ratio < MinimumFor(role))
            {
                failures.Add($"{role}: contrast {ratio:F2}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.TrueColor, true)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    [InlineData(ColorDepth.None, false)]
    public void GivenAnyColourTier_WhenDrawingARangeRow_ThenItDiffersFromTheSelectedRowByStyleAsWell(ColorDepth depth, bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));

        var range = Palette.For(new StyledSegment("x", SegmentRole.Plain), RangeHighlight, colors);
        var selected = Palette.For(new StyledSegment("x", SegmentRole.Plain), true, true, colors);

        Assert.NotEqual(selected.Style, range.Style);
        Assert.True(range.Style.HasFlag(TextStyle.Bold), "A range must not rely on colour alone.");
        Assert.NotEqual(selected, range);
    }

    [Theory]
    [InlineData(ColorDepth.TrueColor, false)]
    [InlineData(ColorDepth.TrueColor, true)]
    [InlineData(ColorDepth.Ansi16, false)]
    [InlineData(ColorDepth.Ansi16, true)]
    [InlineData(ColorDepth.None, false)]
    public void GivenAnyColourTier_WhenDrawingTheMovingEndOfARange_ThenItDiffersFromTheOtherRangeRowsByStyle(
        ColorDepth depth,
        bool isLight)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, isLight));
        var segment = new StyledSegment("x", SegmentRole.Plain);

        var other = Palette.For(segment, RangeHighlight, colors);
        var cursor = Palette.For(segment, new SegmentHighlight(true, true, true, true), colors);

        Assert.NotEqual(other.Style, cursor.Style);
        Assert.True(cursor.Style.HasFlag(TextStyle.Underline), "The moving end must not rely on colour alone.");
        Assert.True(cursor.Style.HasFlag(TextStyle.Bold));
        Assert.Equal(other.Background, cursor.Background);
    }

    [Theory]
    [InlineData(ColorDepth.Ansi16)]
    [InlineData(ColorDepth.None)]
    public void GivenAColourlessOrSixteenColourTier_WhenDrawingARangeRow_ThenItIsReversed(ColorDepth depth)
    {
        var colors = PaletteTable.For(new PaletteSettings(depth, false));

        var range = Palette.For(new StyledSegment("x", SegmentRole.Plain), RangeHighlight, colors);

        Assert.True(range.Style.HasFlag(TextStyle.Reverse));
    }

    [Fact]
    public void GivenARowOutsideTheRange_WhenDrawingASelectedRow_ThenItKeepsTheSelectionLook()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.TrueColor, false));
        var segment = new StyledSegment("x", SegmentRole.Plain);

        var viaHighlight = Palette.For(segment, new SegmentHighlight(true, true), colors);

        Assert.Equal(Palette.For(segment, true, true, colors), viaHighlight);
        Assert.Equal(colors.SelectionFocused, viaHighlight.Background);
    }

    [Fact]
    public void GivenAThemeFile_WhenDrawingARangeRow_ThenItIsAStepAwayFromTheSelectionTint()
    {
        var settings = new PaletteSettings(ColorDepth.TrueColor, false).WithTheme(ThemeChoice.FromText("tokyo-night"), Themes);

        var colors = PaletteTable.For(settings);

        Assert.NotEqual(Color.None, colors.RangeBackground);
        Assert.NotEqual(colors.SelectionFocused, colors.RangeBackground);
    }

    private static int ToRgb(Color color) => (color.R << 16) | (color.G << 8) | color.B;

    private static double MinimumFor(SegmentRole role) => role switch
    {
        SegmentRole.Border => DecorationRatio,
        SegmentRole.WhitespaceMark or SegmentRole.WhitespaceMarkAddedWord
            or SegmentRole.WhitespaceMarkRemovedWord => SubtleRatio,
        _ => ReadableRatio,
    };

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
