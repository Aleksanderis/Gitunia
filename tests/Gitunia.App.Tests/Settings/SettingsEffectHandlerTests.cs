using Gitunia.App.Settings;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Xunit;

namespace Gitunia.App.Tests.Settings;

// Real files in a temporary folder: the handler is the only place that reads and writes config.json for the screen.
public sealed class SettingsEffectHandlerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("gitunia-settings-handler-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task GivenNoFile_WhenReadingToOpen_ThenTheDefaultsAreParsedAndTheKeyReferenceIsBuilt()
    {
        var result = await RunAsync(new ReadSettingsEffect(SettingsReadIntent.Open),
            TestContext.Current.CancellationToken);

        var read = Assert.IsType<SettingsFileRead>(result);
        Assert.False(read.Read.Snapshot.Exists);
        Assert.Equal(SettingsReadIntent.Open, read.Intent);
        Assert.Equal(SettingsRegistry.All, read.Definitions);
        Assert.NotEmpty(read.KeyReference);
        Assert.Equal(string.Empty, read.Read.Load.Banner);
    }

    [Fact]
    public async Task GivenAFileWithValues_WhenReading_ThenTheValuesAreParsed()
    {
        WriteConfig("{ \"tabs\": { \"showBranch\": true } }");

        var result = await RunAsync(new ReadSettingsEffect(SettingsReadIntent.Reload),
            TestContext.Current.CancellationToken);

        var read = Assert.IsType<SettingsFileRead>(result);
        Assert.True(read.Read.Load.Options.ShowBranchInTab);
        Assert.Empty(read.KeyReference);
    }

    [Fact]
    public async Task GivenAFileThatDoesNotParse_WhenReading_ThenTheBannerSaysWhyAndNothingIsRaised()
    {
        WriteConfig("{ not json");

        var result = await RunAsync(new ReadSettingsEffect(SettingsReadIntent.Open),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(string.Empty, Assert.IsType<SettingsFileRead>(result).Read.Load.Banner);
    }

    [Fact]
    public async Task GivenNoConfigFolder_WhenWritingTheFirstChange_ThenTheFileIsCreatedWithOnlyThatKey()
    {
        var commit = Commit(SettingsRegistry.ShowBranchId, SettingValue.OfFlag(true));
        var snapshot = new ConfigSnapshot(false, string.Empty, string.Empty);

        var result = await RunAsync(new WriteSettingEffect(commit, "true", snapshot),
            TestContext.Current.CancellationToken);

        var written = Assert.IsType<SettingsWritten>(result);
        Assert.Equal(ConfigWriteStatus.Written, written.Result.Status);
        Assert.Equal("{\n  \"tabs\": { \"showBranch\": true }\n}\n", File.ReadAllText(ConfigPath));
        Assert.True(written.Read!.Load.Options.ShowBranchInTab);
        Assert.Equal(commit, written.Commit);
    }

    [Fact]
    public async Task GivenTheFileChangedAfterItWasRead_WhenWriting_ThenNothingIsOverwrittenAndTheNewValuesCome()
    {
        WriteConfig("{ \"theme\": \"light\" }");
        var read = Assert.IsType<SettingsFileRead>(
            await RunAsync(new ReadSettingsEffect(SettingsReadIntent.Open), TestContext.Current.CancellationToken));
        WriteConfig("{ \"theme\": \"light\", \"tabs\": { \"showBranch\": true } }");

        var result = await RunAsync(new WriteSettingEffect(
            Commit(SettingsRegistry.ShowBranchId, SettingValue.OfFlag(false)),
            "false",
            read.Read.Snapshot), TestContext.Current.CancellationToken);

        var written = Assert.IsType<SettingsWritten>(result);
        Assert.Equal(ConfigWriteStatus.Stale, written.Result.Status);
        Assert.Contains("changed on disk", written.Result.Message, StringComparison.Ordinal);
        Assert.Equal("{ \"theme\": \"light\", \"tabs\": { \"showBranch\": true } }", File.ReadAllText(ConfigPath));
        Assert.True(written.Read!.Load.Options.ShowBranchInTab);
    }

    [Fact]
    public async Task GivenAFileThatCannotBeEdited_WhenWriting_ThenTheWriteIsRefusedAndTheFileIsKept()
    {
        WriteConfig("{ not json");
        var read = Assert.IsType<SettingsFileRead>(
            await RunAsync(new ReadSettingsEffect(SettingsReadIntent.Open), TestContext.Current.CancellationToken));

        var result = await RunAsync(new WriteSettingEffect(
            Commit(SettingsRegistry.ShowBranchId, SettingValue.OfFlag(true)),
            "true",
            read.Read.Snapshot), TestContext.Current.CancellationToken);

        var written = Assert.IsType<SettingsWritten>(result);
        Assert.NotEqual(ConfigWriteStatus.Written, written.Result.Status);
        Assert.Equal("{ not json", File.ReadAllText(ConfigPath));
        Assert.Null(written.Read);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReading_ThenTheResultIsCancellation()
    {
        WriteConfig("{ }");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var result = await RunAsync(new ReadSettingsEffect(SettingsReadIntent.Open), source.Token);

        Assert.IsType<SettingsOperationCancelled>(result);
    }

    private string ConfigPath => Path.Combine(_root, "config", ConfigLoader.FileName);

    private void WriteConfig(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, text);
    }

    private static SettingsCommit Commit(string id, SettingValue value) =>
        new(SettingsRegistry.All.First(definition => definition.Id == id), value);

    private async Task<AppCommand> RunAsync(SettingsEffect effect, CancellationToken cancellationToken)
    {
        var services = new SettingsServices(
            new ConfigWriter(Path.Combine(_root, "config")),
            new SettingsEnvironment(_root, Directory.Exists),
            SettingsRegistry.All,
            DefaultKeymap.Create());
        var dispatcher = new RecordingDispatcher();
        new SettingsEffectHandler(services, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
