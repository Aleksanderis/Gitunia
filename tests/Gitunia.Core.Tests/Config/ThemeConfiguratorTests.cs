using System.Text.Json;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public sealed class ThemeConfiguratorTests
{
    [Theory]
    [InlineData("\"auto\"", ThemeChoice.Auto)]
    [InlineData("\"dark\"", ThemeChoice.Dark)]
    [InlineData("\"light\"", ThemeChoice.Light)]
    [InlineData("\" Light \"", ThemeChoice.Light)]
    [InlineData("\"DARK\"", ThemeChoice.Dark)]
    [InlineData("null", ThemeChoice.Auto)]
    public void GivenAValidValue_WhenParsing_ThenTheChoiceIsReturnedWithoutProblems(string json, ThemeChoice expected)
    {
        var problems = new List<ConfigProblem>();

        var choice = ThemeConfigurator.Parse(JsonDocument.Parse(json).RootElement, problems);

        Assert.Equal(expected, choice);
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
}
