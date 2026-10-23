using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;
using Xunit;

namespace Gitunia.Core.Tests.Themes;

public sealed class ThemeCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gitunia-themes-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GivenEveryListedBuiltInName_WhenLoadingIt_ThenItIsValidAndEmbedded()
    {
        var catalog = new ThemeCatalog(_directory);
        var problems = new List<ConfigProblem>();

        foreach (var name in BuiltInThemes.Names)
        {
            Assert.NotNull(catalog.Find(name, problems));
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void GivenTheEmbeddedFiles_WhenComparingWithTheListedNames_ThenTheyAreTheSameSet()
    {
        Assert.Equal(BuiltInThemes.EmbeddedNames, BuiltInThemes.Names.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GivenNoThemesFolder_WhenListingNames_ThenOnlyTheBuiltInOnesAreOffered()
    {
        Assert.Equal(BuiltInThemes.Names, new ThemeCatalog(_directory).Names);
    }

    [Fact]
    public void GivenAUserFile_WhenListingAndFinding_ThenItIsOfferedByItsLowerCaseFileName()
    {
        WriteTheme("My-Theme.json", "My Theme");
        var catalog = new ThemeCatalog(_directory);

        var found = catalog.Find("MY-theme", []);

        Assert.Contains("my-theme", catalog.Names);
        Assert.Equal("My Theme", found?.Name);
    }

    [Fact]
    public void GivenAUserFileNamedLikeABuiltInTheme_WhenFinding_ThenTheUserFileWinsAndTheNameIsListedOnce()
    {
        WriteTheme("tokyo-night.json", "Mine");
        var catalog = new ThemeCatalog(_directory);

        Assert.Equal("Mine", catalog.Find("tokyo-night", [])?.Name);
        Assert.Single(catalog.Names, "tokyo-night");
    }

    [Theory]
    [InlineData("dark.json")]
    [InlineData("auto.json")]
    [InlineData("Light.json")]
    [InlineData("notes.txt")]
    public void GivenAFileThatCannotBeAThemeName_WhenListingNames_ThenItIsIgnored(string fileName)
    {
        WriteTheme(fileName, "x");

        Assert.Equal(BuiltInThemes.Names, new ThemeCatalog(_directory).Names);
    }

    [Fact]
    public void GivenAnInvalidUserFile_WhenListingNames_ThenItIsNotOfferedAndItsProblemIsReported()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "broken.json"), "{ nope");
        var catalog = new ThemeCatalog(_directory);

        Assert.DoesNotContain("broken", catalog.Names);
        Assert.Null(catalog.Find("broken", []));
        Assert.Single(catalog.Problems);
    }

    [Fact]
    public void GivenTwoFilesWithTheSameLowerCaseName_WhenListing_ThenTheFirstInOrdinalOrderWins()
    {
        WriteTheme("B.json", "Upper");
        WriteTheme("b.json", "Lower");

        var found = new ThemeCatalog(_directory).Find("b", []);

        Assert.True(found?.Name is "Upper" or "Lower");
        Assert.Equal("Upper", found?.Name);
    }

    [Fact]
    public void GivenAnUnknownName_WhenFinding_ThenNothingIsReturnedWithoutProblems()
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(new ThemeCatalog(_directory).Find("nothing", problems));
        Assert.Empty(problems);
    }

    private void WriteTheme(string fileName, string name)
    {
        Directory.CreateDirectory(_directory);
        const string colors = """
            "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7", "warn": "#f9e2af",
            "info": "#89dceb", "muted": "#9399b2", "border": "#6c7086", "barBackground": "#181825",
            "selectionFocused": "#313244", "selectionDim": "#262637", "chipForeground": "#11111b",
            "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e", "lanes": ["#89b4fa"]
            """;
        File.WriteAllText(
            Path.Combine(_directory, fileName),
            $$"""{ "name": "{{name}}", "variant": "dark", "colors": { {{colors}} } }""");
    }
}
