using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class SettingsValueReaderTests
{
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;
    private static readonly string Home = Path.Combine(Root, "home", "user");
    private static readonly string Srv = Path.Combine(Root, "srv");
    private static readonly string Missing = Path.Combine(Root, "missing");

    private static string Json(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(true, "  \n ")]
    public void GivenNoFileOrABlankOne_WhenReading_ThenTheDefaultsShowAndTheFileIsEditable(bool exists, string text)
    {
        var load = Read(new ConfigSnapshot(exists, text, string.Empty));

        Assert.Equal(string.Empty, load.Banner);
        Assert.Equal("auto", load.Values[SettingsRegistry.ThemeId].Choice);
        Assert.False(load.Values[SettingsRegistry.ShowBranchId].Flag);
        Assert.True(load.Values[SettingsRegistry.ScanRootsId].IsDefault);
        Assert.Equal(ConfigOptions.Default, load.Options);
        Assert.Empty(load.Warnings);
    }

    [Theory]
    [InlineData("{}", "auto", ThemeChoice.Auto)]
    [InlineData("{ \"theme\": \"dark\" }", "dark", ThemeChoice.Dark)]
    [InlineData("{ \"theme\": \"Light\" }", "light", ThemeChoice.Light)]
    public void GivenATheme_WhenReading_ThenTheChoiceAndTheRunningThemeHoldIt(
        string text,
        string choice,
        ThemeChoice expected)
    {
        var load = Read(new ConfigSnapshot(true, text, "h"));

        Assert.Equal(choice, load.Values[SettingsRegistry.ThemeId].Choice);
        Assert.Equal(expected, load.Theme);
        Assert.Empty(load.Warnings);
    }

    [Fact]
    public void GivenAnUnknownTheme_WhenReading_ThenAutoShowsWithAWarningOnTheThemeRow()
    {
        var load = Read(new ConfigSnapshot(true, "{ \"theme\": \"solarized\" }", "h"));

        Assert.Equal("auto", load.Values[SettingsRegistry.ThemeId].Choice);
        Assert.Equal(ThemeChoice.Auto, load.Theme);
        Assert.Contains("solarized", load.Warnings[SettingsRegistry.ThemeId], StringComparison.Ordinal);
        Assert.Equal(string.Empty, load.Banner);
    }

    [Fact]
    public void GivenAFileWithValues_WhenReading_ThenTheScreenAndTheOptionsHoldThem()
    {
        var text =
            "{ \"tabs\": { \"showBranch\": true }, "
            + $"\"scanRoots\": [\"~/Projects\", \"{Json(Srv)}\"] }}";

        var load = Read(new ConfigSnapshot(true, text, "h"));

        Assert.True(load.Values[SettingsRegistry.ShowBranchId].Flag);
        Assert.Equal(["~/Projects", Srv], load.Values[SettingsRegistry.ScanRootsId].Paths);
        Assert.False(load.Values[SettingsRegistry.ScanRootsId].IsDefault);
        Assert.True(load.Options.ShowBranchInTab);
        Assert.Equal([Path.Combine(Home, "Projects"), Srv], load.Options.ScanRoots);
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
        var load = Read(new ConfigSnapshot(true, "{\n  \"theme\": \n", "h"));

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
        var text = $"{{ \"theme\": \"round\", \"tabs\": 5, \"scanRoots\": [\"{Json(Srv)}\", \"{Json(Missing)}\"] }}";

        var load = Read(new ConfigSnapshot(true, text, "h"));

        Assert.Contains("theme", load.Warnings[SettingsRegistry.ThemeId], StringComparison.Ordinal);
        Assert.Contains("tabs", load.Warnings[SettingsRegistry.ShowBranchId], StringComparison.Ordinal);
        Assert.Contains(Missing, load.Warnings[SettingsRegistry.ScanRootsId], StringComparison.Ordinal);
        Assert.Equal("auto", load.Values[SettingsRegistry.ThemeId].Choice);
        Assert.False(load.Values[SettingsRegistry.ShowBranchId].Flag);
        Assert.Equal([Srv, Missing], load.Values[SettingsRegistry.ScanRootsId].Paths);
    }

    private static SettingsLoad Read(ConfigSnapshot snapshot) =>
        SettingsValueReader.Read(snapshot, Home, path => !path.Contains("missing", StringComparison.Ordinal));
}
