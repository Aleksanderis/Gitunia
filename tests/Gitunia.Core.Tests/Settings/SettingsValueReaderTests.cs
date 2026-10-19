using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class SettingsValueReaderTests
{
    private const string Home = "/home/user";

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(true, "  \n ")]
    public void GivenNoFileOrABlankOne_WhenReading_ThenTheDefaultsShowAndTheFileIsEditable(bool exists, string text)
    {
        var load = Read(new ConfigSnapshot(exists, text, string.Empty));

        Assert.Equal(string.Empty, load.Banner);
        Assert.Equal("rounded", load.Values[SettingsRegistry.FrameStyleId].Choice);
        Assert.False(load.Values[SettingsRegistry.ShowBranchId].Flag);
        Assert.True(load.Values[SettingsRegistry.ScanRootsId].IsDefault);
        Assert.Equal(ConfigOptions.Default.FrameStyle, load.Options.FrameStyle);
        Assert.Empty(load.Warnings);
    }

    [Fact]
    public void GivenAFileWithValues_WhenReading_ThenTheScreenAndTheOptionsHoldThem()
    {
        const string text = "{ \"frameStyle\": \"Square\", \"tabs\": { \"showBranch\": true }, \"scanRoots\": [\"~/Projects\", \"/srv\"] }";

        var load = Read(new ConfigSnapshot(true, text, "h"));

        Assert.Equal("square", load.Values[SettingsRegistry.FrameStyleId].Choice);
        Assert.True(load.Values[SettingsRegistry.ShowBranchId].Flag);
        Assert.Equal(["~/Projects", "/srv"], load.Values[SettingsRegistry.ScanRootsId].Paths);
        Assert.False(load.Values[SettingsRegistry.ScanRootsId].IsDefault);
        Assert.Equal(FrameStyle.Square, load.Options.FrameStyle);
        Assert.True(load.Options.ShowBranchInTab);
        Assert.Equal([$"{Home}/Projects", "/srv"], load.Options.ScanRoots);
    }

    [Fact]
    public void GivenAnEmptyScanRootsList_WhenReading_ThenItIsAnExplicitEmptyListNotTheDefault()
    {
        var load = Read(new ConfigSnapshot(true, "{ \"scanRoots\": [] }", "h"));

        Assert.False(load.Values[SettingsRegistry.ScanRootsId].IsDefault);
        Assert.Empty(load.Values[SettingsRegistry.ScanRootsId].Paths);
    }

    [Fact]
    public void GivenInvalidJson_WhenReading_ThenTheBannerNamesTheProblemAndItsLine()
    {
        var load = Read(new ConfigSnapshot(true, "{\n  \"frameStyle\": \n", "h"));

        Assert.StartsWith("config.json cannot be edited:", load.Banner, StringComparison.Ordinal);
        Assert.Contains("line", load.Banner, StringComparison.Ordinal);
        Assert.Equal(ConfigOptions.Default, load.Options);
    }

    [Fact]
    public void GivenAnUnreadableFile_WhenReading_ThenTheBannerCarriesTheReason()
    {
        var load = Read(new ConfigSnapshot(true, string.Empty, string.Empty, "config.json is too large to edit"));

        Assert.Equal("config.json cannot be edited: config.json is too large to edit", load.Banner);
    }

    [Fact]
    public void GivenBadValues_WhenReading_ThenEachSettingGetsAWarningAndItsDefault()
    {
        const string text = "{ \"frameStyle\": \"round\", \"tabs\": 5, \"scanRoots\": [\"/srv\", \"/missing\"] }";

        var load = Read(new ConfigSnapshot(true, text, "h"));

        Assert.Contains("frameStyle", load.Warnings[SettingsRegistry.FrameStyleId], StringComparison.Ordinal);
        Assert.Contains("tabs", load.Warnings[SettingsRegistry.ShowBranchId], StringComparison.Ordinal);
        Assert.Contains("/missing", load.Warnings[SettingsRegistry.ScanRootsId], StringComparison.Ordinal);
        Assert.Equal("rounded", load.Values[SettingsRegistry.FrameStyleId].Choice);
        Assert.False(load.Values[SettingsRegistry.ShowBranchId].Flag);
        Assert.Equal(["/srv", "/missing"], load.Values[SettingsRegistry.ScanRootsId].Paths);
    }

    private static SettingsLoad Read(ConfigSnapshot snapshot) =>
        SettingsValueReader.Read(snapshot, Home, path => !path.Contains("missing", StringComparison.Ordinal));
}
