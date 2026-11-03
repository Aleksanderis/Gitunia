using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core;
using Gitunia.Platform;
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

        var built = new ConfigStartup(_directory, _directory).LoadKeymap();

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

        var built = new ConfigStartup(_directory, _directory).LoadKeymap();

        Assert.Single(built.Problems);
        Assert.Equal(DefaultKeymap.Create().Bindings.Count, built.Table.Bindings.Count);
    }

    [Fact]
    public void GivenNoConfig_WhenLoadingTheKeymap_ThenThereAreNoProblems()
    {
        Assert.Empty(new ConfigStartup(_directory, _directory).LoadKeymap().Problems);
    }

    [Fact]
    public void GivenMoreProblemsThanTheLimit_WhenReporting_ThenTheFirstShowsLastWithACount()
    {
        var statuses = new StatusReporter(new Dispatcher(AppState.Empty));
        var problems = Enumerable.Range(0, 8).Select(index => new ConfigProblem($"keys[{index}]", "bad")).ToList();

        ConfigStartup.Report(problems, statuses);

        Assert.Equal(5, statuses.Log.History.Count);
        Assert.StartsWith("Config: keys[0]: bad (and 7 more", statuses.Current!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenOneProblem_WhenReporting_ThenOneWarningWithoutACountIsPosted()
    {
        var statuses = new StatusReporter(new Dispatcher(AppState.Empty));

        ConfigStartup.Report([new ConfigProblem(string.Empty, "config.json is not valid")], statuses);

        Assert.Equal("Config: config.json is not valid", statuses.Current!.Text);
        Assert.Equal(StatusSeverity.Warning, statuses.Current.Severity);
    }

    [Theory]
    [InlineData("""{ "theme": "light" }""", "light")]
    [InlineData("""{ "theme": "Dark" }""", "dark")]
    [InlineData("""{ "theme": "auto" }""", "auto")]
    [InlineData("""{ "keys": [] }""", "auto")]
    public void GivenAThemeSetting_WhenLoading_ThenTheChoiceFollowsItWithoutProblems(string json, string expected)
    {
        Write(json);

        var config = new ConfigStartup(_directory, _directory).Load();

        Assert.Equal(expected, config.Theme.Name);
        Assert.Empty(config.Keymap.Problems);
    }

    [Fact]
    public void GivenAnUnknownTheme_WhenLoading_ThenAutoApplies_AndTheProblemNamesTheSetting()
    {
        Write("""{ "theme": "solarized" }""");

        var config = new ConfigStartup(_directory, _directory).Load();

        Assert.Equal(ThemeChoice.Auto, config.Theme);
        Assert.Equal("theme", Assert.Single(config.Keymap.Problems).Path);
    }

    [Fact]
    public void GivenOptionsInTheFile_WhenLoading_ThenTheyReachTheStartupConfig()
    {
        var root = _directory.Replace("\\", "\\\\", StringComparison.Ordinal);
        Write($$"""
            { "frameStyle": "square", "tabs": { "showBranch": true }, "scanRoots": ["{{root}}"] }
            """);

        var options = new ConfigStartup(_directory, _directory).Load().Options;

        Assert.True(options.ShowBranchInTab);
        Assert.Equal([_directory], options.ScanRoots);
    }

    [Fact]
    public void GivenAnyConsole_WhenLoadingWithoutLabelCaps_ThenRoundedAndAnExplicitValueWins()
    {
        var classic = new ConsoleCapabilities(true, ColorDepth.Ansi16, true, IsClassicConsole: true);
        var modern = classic with { IsClassicConsole = false };
        Assert.Equal(LabelCapStyle.Rounded, ConfigStartup.DefaultLabelCaps(classic));
        Assert.Equal(LabelCapStyle.Rounded, ConfigStartup.DefaultLabelCaps(modern));

        Assert.Equal(LabelCapStyle.Rounded, new ConfigStartup(_directory, _directory, ConfigStartup.DefaultLabelCaps(classic)).Load().Options.LabelCaps);

        Write("""{ "labelCaps": "angled" }""");
        Assert.Equal(LabelCapStyle.Angled, new ConfigStartup(_directory, _directory, ConfigStartup.DefaultLabelCaps(classic)).Load().Options.LabelCaps);
    }

    [Fact]
    public void GivenBadOptions_WhenLoading_ThenDefaultsApplyAndEveryProblemIsReported()
    {
        Write("""{ "tabs": 3, "scanRoots": "x" }""");

        var config = new ConfigStartup(_directory, _directory).Load();

        Assert.Equal(ConfigOptions.Default, config.Options);
        Assert.Equal(["tabs", "scanRoots"], config.Keymap.Problems.Select(problem => problem.Path));
    }

    [Fact]
    public void GivenABuiltInThemeName_WhenLoading_ThenItIsChosenWithoutProblems()
    {
        Write("""{ "theme": "catppuccin-frappe" }""");

        var config = new ConfigStartup(_directory, _directory).Load();

        Assert.Equal("catppuccin-frappe", config.Theme.Name);
        Assert.Empty(config.Keymap.Problems);
    }

    [Fact]
    public void GivenAUserThemeFile_WhenLoading_ThenItsNameIsAcceptedAndOffered()
    {
        var themes = Directory.CreateDirectory(Path.Combine(_directory, "themes")).FullName;
        File.WriteAllText(Path.Combine(themes, "mine.json"), UserTheme);
        Write("""{ "theme": "Mine" }""");

        var config = new ConfigStartup(_directory, _directory).Load();

        Assert.Equal("mine", config.Theme.Name);
        Assert.Contains("mine", config.Themes.Names);
        Assert.Empty(config.Keymap.Problems);
    }

    [Fact]
    public void GivenABrokenUserThemeFile_WhenLoading_ThenAutoAppliesWithTheFilesProblems()
    {
        var themes = Directory.CreateDirectory(Path.Combine(_directory, "themes")).FullName;
        File.WriteAllText(
            Path.Combine(themes, "mine.json"),
            """{ "variant": "dark", "colors": { "lanes": ["#000000"] } }""");
        Write("""{ "theme": "mine" }""");

        var config = new ConfigStartup(_directory, _directory).Load();

        Assert.Equal(ThemeChoice.Auto, config.Theme);
        Assert.Contains(
            config.Keymap.Problems,
            problem => problem.Path.StartsWith("mine: colors.accent", StringComparison.Ordinal));
        Assert.Contains(config.Keymap.Problems, problem => problem.Path == "theme");
    }

    private const string UserTheme = """
        { "name": "Mine", "variant": "dark", "colors": {
          "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7", "warn": "#f9e2af",
          "info": "#89dceb", "muted": "#9399b2", "border": "#6c7086", "barBackground": "#181825",
          "selectionFocused": "#313244", "selectionDim": "#262637", "chipForeground": "#11111b",
          "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e", "lanes": ["#89b4fa"] } }
        """;

    private void Write(string content) => File.WriteAllText(Path.Combine(_directory, ConfigLoader.FileName), content);
}
