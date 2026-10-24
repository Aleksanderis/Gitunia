using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;
using Gitunia.Core.Themes.Models;
using Xunit;

namespace Gitunia.Core.Tests.Themes;

public sealed class ThemeFileParserTests
{
    [Fact]
    public void GivenAMinimalFile_WhenParsing_ThenOptionalRolesTakeTheirDefaults()
    {
        var problems = new List<ConfigProblem>();

        var theme = ThemeFileParser.Parse("mine", Json(), problems);

        Assert.Empty(problems);
        Assert.NotNull(theme);
        Assert.Equal("Mine", theme.Name);
        Assert.False(theme.IsLight);
        Assert.Equal(0x111111, theme.Colors[ThemeRoles.Muted]);
        Assert.Equal(0x111111, theme.Colors[ThemeRoles.MutedOnSelection]);
        Assert.Equal(0x0a0a0b, theme.Colors[ThemeRoles.Subtle]);
        Assert.Equal(0x222222, theme.Colors[ThemeRoles.TabButtonBackground]);
        Assert.Equal(0x010203, theme.Colors[ThemeRoles.ChipBackground]);
        Assert.Equal(0x040506, theme.Colors[ThemeRoles.MatchBackground]);
        Assert.False(theme.Colors.ContainsKey(ThemeRoles.GroupBackground));
        Assert.Equal([0x010203, 0x040506], theme.Lanes);
        Assert.Empty(theme.Ansi);
    }

    [Fact]
    public void GivenCommentsAndTrailingCommas_WhenParsing_ThenTheFileIsAccepted()
    {
        var problems = new List<ConfigProblem>();

        var theme = ThemeFileParser.Parse("mine", "// note\n" + Json(extra: "\"version\": 1,"), problems);

        Assert.NotNull(theme);
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenALightVariantWithAnsiOverrides_WhenParsing_ThenTheyAreKeptWithCanonicalNames()
    {
        var problems = new List<ConfigProblem>();

        var ansi = "\"ansi\": { \"Accent\": \"BRIGHTCYAN\" },";

        var theme = ThemeFileParser.Parse("mine", Json(variant: "Light", ansi: ansi), problems);

        Assert.NotNull(theme);
        Assert.True(theme.IsLight);
        Assert.Equal("brightCyan", theme.Ansi[ThemeRoles.Accent]);
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public void GivenAFileThatIsNotAnObject_WhenParsing_ThenNothingIsReturnedWithAProblem(string text)
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(ThemeFileParser.Parse("mine", text, problems));
        Assert.Single(problems);
    }

    [Fact]
    public void GivenAMissingRole_WhenParsing_ThenTheProblemNamesIt()
    {
        var problems = new List<ConfigProblem>();

        var text = Json().Replace("\"accent\": \"#010203\",", string.Empty, StringComparison.Ordinal);

        var theme = ThemeFileParser.Parse("mine", text, problems);

        Assert.Null(theme);
        Assert.Contains("colors.accent", Assert.Single(problems).Path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("123456")]
    [InlineData("#12345g")]
    [InlineData("blue")]
    public void GivenABadColour_WhenParsing_ThenTheFileIsRefusedWithAProblemOnTheRole(string colour)
    {
        var problems = new List<ConfigProblem>();

        var text = Json().Replace("\"accent\": \"#010203\"", $"\"accent\": \"{colour}\"", StringComparison.Ordinal);

        var theme = ThemeFileParser.Parse("mine", text, problems);

        Assert.Null(theme);
        Assert.Contains("colors.accent", Assert.Single(problems).Path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sepia")]
    [InlineData("")]
    public void GivenAnUnknownVariant_WhenParsing_ThenTheFileIsRefused(string variant)
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(ThemeFileParser.Parse("mine", Json(variant: variant), problems));
        Assert.Contains("variant", Assert.Single(problems).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnknownFormatVersion_WhenParsing_ThenTheFileIsRefused()
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(ThemeFileParser.Parse("mine", Json(extra: "\"version\": 2,"), problems));
        Assert.Contains("version", Assert.Single(problems).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"ansi\": { \"accent\": \"teal\" },")]
    [InlineData("\"ansi\": { \"lanes\": \"red\" },")]
    [InlineData("\"ansi\": { \"accent\": 4 },")]
    public void GivenABadAnsiMapping_WhenParsing_ThenTheFileIsRefusedWithAProblemOnTheRole(string ansi)
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(ThemeFileParser.Parse("mine", Json(ansi: ansi), problems));
        Assert.StartsWith("mine: ansi.", Assert.Single(problems).Path, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenSubtleRoles_WhenParsing_ThenTheFileValuesWinOverTheDerivedOnes()
    {
        var problems = new List<ConfigProblem>();
        var text = Json().Replace(
            "\"muted\": \"#111111\",",
            "\"muted\": \"#111111\", \"subtle\": \"#0a0b0c\", \"subtleOnSelection\": \"#0d0e0f\",",
            StringComparison.Ordinal);

        var theme = ThemeFileParser.Parse("mine", text, problems);

        Assert.NotNull(theme);
        Assert.Equal(0x0a0b0c, theme.Colors[ThemeRoles.Subtle]);
        Assert.Equal(0x0d0e0f, theme.Colors[ThemeRoles.SubtleOnSelection]);
    }

    [Fact]
    public void GivenMutedOnSelection_WhenParsing_ThenSubtleOnSelectionMixesItIntoTheSelection()
    {
        var problems = new List<ConfigProblem>();
        var text = Json().Replace(
            "\"muted\": \"#111111\",",
            "\"muted\": \"#111111\", \"mutedOnSelection\": \"#ffffff\",",
            StringComparison.Ordinal);

        var theme = ThemeFileParser.Parse("mine", text, problems);

        // 70% of white into the selection #222222: 0.7 * 255 + 0.3 * 34 = 188.7.
        Assert.NotNull(theme);
        Assert.Equal(0xbdbdbd, theme.Colors[ThemeRoles.SubtleOnSelection]);
    }

    [Fact]
    public void GivenNoLanes_WhenParsing_ThenTheFileIsRefused()
    {
        var problems = new List<ConfigProblem>();

        var theme = ThemeFileParser.Parse("mine", Json(lanes: string.Empty), problems);

        Assert.Null(theme);
        Assert.Contains("lanes", Assert.Single(problems).Path, StringComparison.Ordinal);
    }

    private static string Json(
        string variant = "dark",
        string extra = "",
        string ansi = "",
        string lanes = "\"lanes\": [\"#010203\", \"#040506\"]")
    {
        const string roles = "accent good bad remote warn info border barBackground selectionFocused selectionDim "
            + "chipForeground baseForeground baseBackground";
        var colors = string.Join(' ', roles.Split(' ').Select(role => $"\"{role}\": \"#010203\","));
        colors = colors.Replace("\"warn\": \"#010203\"", "\"warn\": \"#040506\"", StringComparison.Ordinal)
            .Replace(
                "\"selectionFocused\": \"#010203\"",
                "\"selectionFocused\": \"#222222\"",
                StringComparison.Ordinal);

        return $$"""
            {
              {{extra}}
              "name": "Mine",
              "variant": "{{variant}}",
              {{ansi}}
              "colors": {
                {{colors}}
                "muted": "#111111",
                {{lanes}}
              },
            }
            """;
    }
}
