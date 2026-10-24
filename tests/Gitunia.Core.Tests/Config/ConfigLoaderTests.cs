using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Tests.Persistence;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task GivenNoFile_WhenLoading_ThenTheDocumentIsEmptyWithoutProblems()
    {
        var result = await new ConfigLoader(_directory.DirectoryPath).LoadAsync(CancellationToken.None);

        Assert.Null(result.Document.Keys);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public async Task GivenCommentsTrailingCommasAndUnknownSections_WhenLoading_ThenTheKeysAreRead()
    {
        await WriteAsync("""
            {
              // My keys.
              "options": { "accent": "blue" },
              "keys": [
                { "mode": "Normal", "view": "Graph", "key": "J", "action": "list.down", },
              ],
            }
            """);

        var result = await new ConfigLoader(_directory.DirectoryPath).LoadAsync(CancellationToken.None);

        Assert.Empty(result.Problems);
        var entry = Assert.Single(result.Document.Keys!);
        Assert.Equal("J", entry!.Key);
        Assert.Equal("list.down", entry.Action);
    }

    [Fact]
    public void GivenAThemeOfTheWrongType_WhenParsing_ThenTheKeysSurviveAndOnlyTheThemeIsAProblem()
    {
        var problems = new List<ConfigProblem>();
        var text = """{ "theme": { "accent": "blue" }, "keys": [ { "key": "J", "action": "list.down" } ] }""";
        var document = ConfigLoader.Parse(text, problems);

        var choice = ThemeConfigurator.Parse(document.Theme, problems);

        Assert.Equal(ThemeChoice.Auto, choice);
        Assert.Single(document.Keys!);
        Assert.Equal("theme", Assert.Single(problems).Path);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"keys\": 5 }")]
    [InlineData("")]
    [InlineData("null")]
    public async Task GivenAnInvalidFile_WhenLoading_ThenTheDocumentIsEmptyAndAProblemIsReported(string content)
    {
        await WriteAsync(content);

        var result = await new ConfigLoader(_directory.DirectoryPath).LoadAsync(CancellationToken.None);

        Assert.Null(result.Document.Keys);
        Assert.Single(result.Problems);
    }

    [Fact]
    public async Task GivenAnInvalidFile_WhenLoading_ThenTheFileIsLeftUntouched()
    {
        var loader = new ConfigLoader(_directory.DirectoryPath);
        await WriteAsync("{ not json");

        await loader.LoadAsync(CancellationToken.None);

        Assert.Equal("{ not json", await File.ReadAllTextAsync(loader.FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenADirectoryInPlaceOfTheFile_WhenLoading_ThenItDoesNotThrow()
    {
        Directory.CreateDirectory(Path.Combine(_directory.DirectoryPath, ConfigLoader.FileName));

        var result = await new ConfigLoader(_directory.DirectoryPath).LoadAsync(CancellationToken.None);

        Assert.Null(result.Document.Keys);
    }

    [Fact]
    public async Task GivenAFileOverTheSizeLimit_WhenLoading_ThenItIsIgnoredWithAProblem()
    {
        await WriteAsync(new string(' ', (1024 * 1024) + 1));

        var result = await new ConfigLoader(_directory.DirectoryPath).LoadAsync(CancellationToken.None);

        Assert.Contains("too large", Assert.Single(result.Problems).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenInvalidText_WhenParsing_ThenTheProblemNamesTheLine()
    {
        var problems = new List<ConfigProblem>();

        ConfigLoader.Parse("{\n  \"keys\": [\n    oops\n  ]\n}", problems);

        Assert.Contains("line 3", Assert.Single(problems).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"key\": 5, \"action\": \"list.down\" }", "keys[0]: 'key' must be text, not a number")]
    [InlineData("{ \"key\": [\"x\"], \"action\": \"list.down\" }", "keys[0]: 'key' must be text, not a list")]
    [InlineData("{ \"key\": \"J\", \"action\": true }", "keys[0]: 'action' must be text, not a boolean")]
    [InlineData("{ \"Mode\": 1, \"key\": \"J\", \"action\": \"list.down\" }", "keys[0]: 'Mode' must be text, not a number")]
    [InlineData("\"J\"", "keys[0]: the entry must be an object, not text")]
    [InlineData("[1]", "keys[0]: the entry must be an object, not a list")]
    public void GivenAWronglyTypedEntry_WhenBuildingTheKeymap_ThenOnlyThatEntryIsReportedAndTheOthersApply(
        string badEntry,
        string expected)
    {
        var problems = new List<ConfigProblem>();
        var good = "{ \"view\": \"Graph\", \"key\": \"J\", \"action\": \"list.down\" }";

        var document = ConfigLoader.Parse($"{{ \"keys\": [ {badEntry}, {good} ] }}", problems);
        var result = KeymapConfigurator.Apply(DefaultKeymap.Create(), document.Keys);

        Assert.Empty(problems);
        Assert.Equal(expected, Assert.Single(result.Problems).ToString());
        Assert.Contains(
            result.Table.Bindings,
            binding => binding.Action == new ActionId("list.down") && KeyFormatter.Format(binding.Sequence) == "J");
    }

    private async Task WriteAsync(string content)
    {
        var path = Path.Combine(_directory.DirectoryPath, ConfigLoader.FileName);
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
    }
}
