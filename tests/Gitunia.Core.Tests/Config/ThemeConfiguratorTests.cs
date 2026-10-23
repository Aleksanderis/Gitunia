using System.Text.Json;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Xunit;

namespace Gitunia.Core.Tests.Config;

[Trait("Os", "Independent")]
public sealed class ThemeConfiguratorTests
{
    [Theory]
    [InlineData("\"auto\"", "auto")]
    [InlineData("\"dark\"", "dark")]
    [InlineData("\"light\"", "light")]
    [InlineData("\" Light \"", "light")]
    [InlineData("\"DARK\"", "dark")]
    [InlineData("null", "auto")]
    public void GivenAValidValue_WhenParsing_ThenTheChoiceIsReturnedWithoutProblems(string json, string expected)
    {
        var problems = new List<ConfigProblem>();

        var choice = ThemeConfigurator.Parse(JsonDocument.Parse(json).RootElement, problems);

        Assert.Equal(expected, choice.Name);
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenNoValue_WhenParsing_ThenAutoWithoutProblems()
    {
        var problems = new List<ConfigProblem>();

        Assert.Equal(ThemeChoice.Auto, ThemeConfigurator.Parse(null, problems));
        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"solarized\"")]
    [InlineData("5")]
    [InlineData("true")]
    [InlineData("{ \"accent\": \"blue\" }")]
    public void GivenAnInvalidValue_WhenParsing_ThenAutoWithAProblemOnTheTheme(string json)
    {
        var problems = new List<ConfigProblem>();

        var choice = ThemeConfigurator.Parse(JsonDocument.Parse(json).RootElement, problems);

        Assert.Equal(ThemeChoice.Auto, choice);
        Assert.Equal("theme", Assert.Single(problems).Path);
    }

    [Theory]
    [InlineData("\"catppuccin-mocha\"", "catppuccin-mocha")]
    [InlineData("\" Tokyo-Night \"", "tokyo-night")]
    public void GivenABuiltInThemeName_WhenParsing_ThenItIsChosenWithoutProblems(string json, string expected)
    {
        var problems = new List<ConfigProblem>();

        var choice = ThemeConfigurator.Parse(JsonDocument.Parse(json).RootElement, problems);

        Assert.Equal(expected, choice.Name);
        Assert.True(choice.IsNamedTheme);
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenAUserThemeName_WhenParsingWithTheNamesOnOffer_ThenItIsChosen()
    {
        var problems = new List<ConfigProblem>();

        var choice = ThemeConfigurator.Parse(JsonDocument.Parse("\"mine\"").RootElement, problems, ["mine"]);

        Assert.Equal("mine", choice.Name);
        Assert.Empty(problems);
    }
}
