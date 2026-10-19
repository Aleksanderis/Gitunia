using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Tui.App.Config;
using Xunit;

namespace Gitunia.Tui.Tests.App.Config;

public sealed class ConfigStartupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gitunia-tests-{Guid.NewGuid():N}");

    public ConfigStartupTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void GivenAConfigRemappingAKey_WhenLoadingTheKeymap_ThenTheEngineFiresTheNewAction()
    {
        Write("""{ "keys": [ { "view": "Graph", "key": "J", "action": "list.down" } ] }""");

        var built = new ConfigStartup(_directory).LoadKeymap();

        var result = new KeymapEngine(built.Table).Process(
            KeymapState.Initial,
            KeySequenceParser.Parse("J").Keys[0],
            DefaultKeymap.GraphView);
        Assert.Empty(built.Problems);
        Assert.Equal(ActionIds.ListDown, result.Action);
    }

    [Fact]
    public void GivenAnUnparsableConfig_WhenLoadingTheKeymap_ThenTheDefaultsApplyWithAProblem()
    {
        Write("{ broken");

        var built = new ConfigStartup(_directory).LoadKeymap();

        Assert.Single(built.Problems);
        Assert.Equal(DefaultKeymap.Create().Bindings.Count, built.Table.Bindings.Count);
    }

    [Fact]
    public void GivenNoConfig_WhenLoadingTheKeymap_ThenThereAreNoProblems()
    {
        Assert.Empty(new ConfigStartup(_directory).LoadKeymap().Problems);
    }

    [Fact]
    public void GivenMoreProblemsThanTheLimit_WhenReporting_ThenTheFirstShowsLastWithACount()
    {
        var statuses = new StatusChannel();
        var problems = Enumerable.Range(0, 8).Select(index => new ConfigProblem($"keys[{index}]", "bad")).ToList();

        ConfigStartup.Report(problems, statuses);

        Assert.Equal(5, statuses.Log.History.Count);
        Assert.StartsWith("Config: keys[0]: bad (and 7 more", statuses.Current!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenOneProblem_WhenReporting_ThenOneWarningWithoutACountIsPosted()
    {
        var statuses = new StatusChannel();

        ConfigStartup.Report([new ConfigProblem(string.Empty, "config.json is not valid")], statuses);

        Assert.Equal("Config: config.json is not valid", statuses.Current!.Text);
        Assert.Equal(StatusSeverity.Warning, statuses.Current.Severity);
    }

    private void Write(string content) => File.WriteAllText(Path.Combine(_directory, ConfigLoader.FileName), content);
}
