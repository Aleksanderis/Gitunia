using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;
using Gitunia.Core.Themes.Models;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Drawing;
using Xunit;
using StyledSegment = Gitunia.Tui.Rendering.Styling.StyledSegment;

namespace Gitunia.Tui.Tests.Views.Theme;

public sealed class ThemeFileContrastTests
{
    private const double ReadableRatio = 4.5;
    private const double DecorationRatio = 3.0;

    // After a 256-colour terminal rounds every colour to the nearest of the xterm cube and grey ramp.
    private const double QuantizedRatio = 3.0;

    // Whitespace marks: readable but far dimmer than text; rounding to 256 colours may cost them some of the 2:1.
    private const double SubtleRatio = 2.0;
    private const double SubtleQuantizedRatio = 1.6;

    private static readonly int[] CubeLevels = [0, 0x5f, 0x87, 0xaf, 0xd7, 0xff];
    private static readonly ThemeCatalog Themes = new(
        Path.Combine(Path.GetTempPath(), $"gitunia-no-themes-{Guid.NewGuid():N}"));

    public static TheoryData<string> Names => [.. BuiltInThemes.Names];

    public static TheoryData<string> AnsiNames => [.. AnsiColorNames.All];

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenColouringAnyRole_ThenTextIsReadableOnItsBackground(string name)
    {
        var (colors, definition) = TableOf(name);
        var failures = new List<string>();
        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            foreach (var highlight in new SegmentHighlight[] { new(false, false), new(true, true), new(true, false) })
            {
                if (role is SegmentRole.Border && highlight.IsSelected)
                {
                    continue;
                }

                var ratio = RatioOf(role, highlight, colors, definition, 0);
                var minimum = MinimumFor(role);
                if (ratio < minimum)
                {
                    var state = $"selected={highlight.IsSelected}, focused={highlight.IsFocused}";
                    failures.Add($"{role} ({state}): {ratio:F2}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenDrawingWhitespaceMarks_ThenTheyAreDimmerThanFaintTextInEveryState(string name)
    {
        var (colors, definition) = TableOf(name);
        var failures = new List<string>();
        foreach (var inGroup in new[] { false, true })
        {
            foreach (var highlight in new SegmentHighlight[] { new(false, false), new(true, true), new(true, false) })
            {
                var mark = Palette.For(
                    new StyledSegment("·", SegmentRole.WhitespaceMark, InGroup: inGroup),
                    highlight.IsSelected,
                    highlight.IsFocused,
                    colors);
                var faint = Palette.For(
                    new StyledSegment("x", SegmentRole.Faint, InGroup: inGroup),
                    highlight.IsSelected,
                    highlight.IsFocused,
                    colors);
                var background = Resolve(mark.Background, definition, false);
                var markRatio = Contrast(Resolve(mark.Foreground, definition, true), background);
                var faintRatio = Contrast(Resolve(faint.Foreground, definition, true), background);
                if (markRatio < SubtleRatio || markRatio > faintRatio - 1.0)
                {
                    failures.Add($"group={inGroup} selected={highlight.IsSelected} focused={highlight.IsFocused}: "
                        + $"mark {markRatio:F2}, faint {faintRatio:F2}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void GivenSixteenColours_WhenDrawingWhitespaceMarks_ThenTheyAreFaintAndFaintTextIsNot()
    {
        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false));

        var mark = Palette.For(new StyledSegment("·", SegmentRole.WhitespaceMark), false, false, colors);
        var faint = Palette.For(new StyledSegment("x", SegmentRole.Faint), false, false, colors);

        Assert.True(mark.Style.HasFlag(TextStyle.Faint));
        Assert.False(faint.Style.HasFlag(TextStyle.Faint));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenColouringTheCursorGroup_ThenTextIsReadableOnTheGroupTint(string name)
    {
        var (colors, definition) = TableOf(name);
        SegmentRole[] roles =
        [
            SegmentRole.Plain, SegmentRole.Faint, SegmentRole.DiffAdded, SegmentRole.DiffRemoved,
            SegmentRole.DiffGroupBar,
        ];

        foreach (var role in roles)
        {
            var attribute = Palette.For(new StyledSegment("x", role, InGroup: true), false, true, colors);

            Assert.True(
                Contrast(Resolve(attribute.Foreground, definition, true), ToRgb(attribute.Background)) >= ReadableRatio,
                $"{name} {role}");
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenColouringLanes_ThenEveryLaneIsReadableOnTheBase(string name)
    {
        var (colors, definition) = TableOf(name);

        for (var lane = 0; lane < colors.Lanes.Length; lane++)
        {
            Assert.True(
                RatioOf(SegmentRole.Lane, new SegmentHighlight(false, false), colors, definition, lane)
                    >= ReadableRatio,
                $"{name} lane {lane}");
            Assert.True(
                RatioOf(SegmentRole.LaneLabel, new SegmentHighlight(false, false), colors, definition, lane)
                    >= ReadableRatio,
                $"{name} label {lane}");
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenRoundedTo256Colours_ThenEveryBranchLabelStaysReadable(string name)
    {
        var (colors, definition) = TableOf(name);
        var failures = new List<string>();
        for (var lane = 0; lane < colors.Lanes.Length; lane++)
        {
            var attribute = Palette.For(new StyledSegment("x", SegmentRole.LaneLabel, lane), false, false, colors);
            var ratio = Contrast(
                Quantize(Resolve(attribute.Foreground, definition, true)),
                Quantize(Resolve(attribute.Background, definition, false)));
            if (ratio < QuantizedRatio)
            {
                failures.Add($"lane {lane}: {ratio:F2}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenRoundedTo256Colours_ThenRolesStayReadable(string name)
    {
        var (colors, definition) = TableOf(name);
        var failures = new List<string>();
        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            foreach (var highlight in new SegmentHighlight[] { new(false, false), new(true, true), new(true, false) })
            {
                var segment = new StyledSegment("x", role);
                var attribute = Palette.For(segment, highlight.IsSelected, highlight.IsFocused, colors);
                var foreground = Quantize(Resolve(attribute.Foreground, definition, true));
                var background = Quantize(Resolve(attribute.Background, definition, false));
                var ratio = Contrast(foreground, background);
                var isMark = role is SegmentRole.WhitespaceMark
                    or SegmentRole.WhitespaceMarkAddedWord or SegmentRole.WhitespaceMarkRemovedWord;
                var minimum = isMark ? SubtleQuantizedRatio : QuantizedRatio;
                if (ratio < minimum && role is not SegmentRole.Border)
                {
                    failures.Add($"{role} (selected={highlight.IsSelected}): {ratio:F2}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenAtSixteenColours_ThenRefsDifferAndNoTextMatchesItsBackground(string name)
    {
        var definition = DefinitionOf(name);
        var settings = new PaletteSettings(ColorDepth.Ansi16, definition.IsLight, false, definition);
        var colors = PaletteTable.For(settings);

        SegmentRole[] refRoles =
            [SegmentRole.RemoteBranch, SegmentRole.LocalBranch, SegmentRole.Tag, SegmentRole.CurrentBranch];
        var refs = refRoles
            .Select(role => Palette.For(new StyledSegment("x", role), false, false, colors).Foreground)
            .ToArray();

        Assert.Equal(4, refs.Distinct().Count());
        foreach (var role in Enum.GetValues<SegmentRole>())
        {
            var attribute = Palette.For(new StyledSegment("x", role), true, true, colors);
            Assert.True(
                attribute.Foreground == Color.None || attribute.Foreground != attribute.Background,
                $"{name} {role}");
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void GivenABuiltInTheme_WhenChosenByName_ThenItsVariantAndBaseColoursApply(string name)
    {
        var definition = DefinitionOf(name);

        var detected = new PaletteSettings(ColorDepth.TrueColor, !definition.IsLight);

        var settings = detected.WithTheme(new ThemeChoice(name), Themes);
        var table = PaletteTable.For(settings);

        Assert.Equal(definition.IsLight, settings.IsLightBackground);
        Assert.Equal(definition.Colors[ThemeRoles.BaseBackground], ToRgb(table.BaseBackground));
        Assert.Equal(definition.Colors[ThemeRoles.Accent], ToRgb(table.Accent));
    }

    [Fact]
    public void GivenAThemeWithAnsiOverrides_WhenAtSixteenColours_ThenTheOverrideReplacesTheVariantDefault()
    {
        var definition = DefinitionOf("catppuccin-mocha") with
        {
            Ansi = new Dictionary<string, string> { [ThemeRoles.Accent] = "cyan" },
        };

        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false, false, definition));

        Assert.Equal(new Color(ColorName16.Cyan), colors.Accent);
        Assert.Equal(new Color(ColorName16.BrightGreen), colors.Good);
    }

    [Theory]
    [MemberData(nameof(AnsiNames))]
    public void GivenAnyAnsiColourName_WhenAtSixteenColours_ThenTheOverrideResolvesToThatColour(string ansiName)
    {
        var definition = DefinitionOf("catppuccin-mocha") with
        {
            Ansi = new Dictionary<string, string> { [ThemeRoles.Accent] = ansiName },
        };

        var colors = PaletteTable.For(new PaletteSettings(ColorDepth.Ansi16, false, false, definition));

        Assert.Equal(new Color(Enum.Parse<ColorName16>(ansiName, ignoreCase: true)), colors.Accent);
    }

    [Fact]
    public void GivenANameWithoutAFile_WhenApplyingTheTheme_ThenTheDetectedSchemeStays()
    {
        var settings = new PaletteSettings(ColorDepth.TrueColor, true).WithTheme(new ThemeChoice("nothing"), Themes);

        Assert.Null(settings.Definition);
        Assert.True(settings.IsLightBackground);
    }

    private static double MinimumFor(SegmentRole role) => role switch
    {
        SegmentRole.Border => DecorationRatio,
        SegmentRole.WhitespaceMark or SegmentRole.WhitespaceMarkAddedWord
            or SegmentRole.WhitespaceMarkRemovedWord => SubtleRatio,
        _ => ReadableRatio,
    };

    private static ThemeDefinition DefinitionOf(string name) =>
        Themes.Find(name, []) ?? throw new InvalidOperationException(name);

    private static LoadedTheme TableOf(string name)
    {
        var definition = DefinitionOf(name);
        var settings = new PaletteSettings(ColorDepth.TrueColor, definition.IsLight, true, definition);

        return new LoadedTheme(PaletteTable.For(settings), definition);
    }

    private static double RatioOf(
        SegmentRole role,
        SegmentHighlight highlight,
        PaletteTable colors,
        ThemeDefinition definition,
        int lane)
    {
        var segment = new StyledSegment("x", role, lane);
        var attribute = Palette.For(segment, highlight.IsSelected, highlight.IsFocused, colors);
        var foreground = Resolve(attribute.Foreground, definition, true);

        return Contrast(foreground, Resolve(attribute.Background, definition, false));
    }

    private static int Resolve(Color color, ThemeDefinition definition, bool isForeground) => color != Color.None
        ? ToRgb(color)
        : definition.Colors[isForeground ? ThemeRoles.BaseForeground : ThemeRoles.BaseBackground];

    private static int ToRgb(Color color) => (color.R << 16) | (color.G << 8) | color.B;

    private static int Quantize(int rgb)
    {
        var cube = (Nearest(rgb >> 16 & 0xff) << 16) | (Nearest(rgb >> 8 & 0xff) << 8) | Nearest(rgb & 0xff);
        var grey = GreyOf(rgb);

        return Distance(rgb, grey) < Distance(rgb, cube) ? grey : cube;
    }

    private static int Nearest(int channel) => CubeLevels.MinBy(level => Math.Abs(level - channel));

    private static int GreyOf(int rgb)
    {
        var average = (((rgb >> 16) & 0xff) + ((rgb >> 8) & 0xff) + (rgb & 0xff)) / 3;
        var step = Math.Clamp((average - 8 + 5) / 10, 0, 23);
        var level = 8 + (step * 10);

        return (level << 16) | (level << 8) | level;
    }

    private static int Distance(int first, int second)
    {
        var red = ((first >> 16) & 0xff) - ((second >> 16) & 0xff);
        var green = ((first >> 8) & 0xff) - ((second >> 8) & 0xff);
        var blue = (first & 0xff) - (second & 0xff);

        return (red * red) + (green * green) + (blue * blue);
    }

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

    private sealed record LoadedTheme(PaletteTable Table, ThemeDefinition Definition);
}
