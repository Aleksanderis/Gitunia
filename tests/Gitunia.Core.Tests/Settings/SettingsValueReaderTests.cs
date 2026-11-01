using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Themes;
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
    [InlineData("{}", "rounded", LabelCapStyle.Rounded)]
    [InlineData("{ \"labelCaps\": \"none\" }", "none", LabelCapStyle.None)]
    [InlineData("{ \"labelCaps\": \"rounded\" }", "rounded", LabelCapStyle.Rounded)]
    [InlineData("{ \"labelCaps\": \"angled\" }", "angled", LabelCapStyle.Angled)]
    public void GivenLabelCaps_WhenReading_ThenTheChoiceAndTheOptionHoldThem(string text, string choice, LabelCapStyle style)
    {
        var load = Read(new ConfigSnapshot(true, text, string.Empty));

        Assert.Equal(choice, load.Values[SettingsRegistry.LabelCapsId].Choice);
        Assert.Equal(style, load.Options.LabelCaps);
    }

    [Theory]
    [InlineData("{ \"hints\": { \"whichKey\": \"off\", \"statusBar\": \"minimal\" } }", "off", "minimal", false, StatusBarHintMode.Minimal)]
    [InlineData("{ \"hints\": { \"statusBar\": \"off\" } }", "on", "off", true, StatusBarHintMode.Off)]
    [InlineData("{ }", "on", "full", true, StatusBarHintMode.Full)]
    public void GivenHints_WhenReading_ThenTheChoicesAndTheOptionsHoldThem(
        string text,
        string whichKey,
        string statusBar,
        bool popup,
        StatusBarHintMode mode)
    {
        var load = Read(new ConfigSnapshot(true, text, string.Empty));

        Assert.Equal(whichKey, load.Values[SettingsRegistry.WhichKeyId].Choice);
        Assert.Equal(statusBar, load.Values[SettingsRegistry.StatusBarHintsId].Choice);
        Assert.Equal(popup, load.Options.WhichKeyPopup);
        Assert.Equal(mode, load.Options.StatusBarHints);
    }

    [Fact]
    public void GivenABadStatusBarValue_WhenReading_ThenTheSettingCarriesTheWarning()
    {
        var load = Read(new ConfigSnapshot(true, "{ \"hints\": { \"statusBar\": \"some\" } }", string.Empty));

        Assert.Equal("full", load.Values[SettingsRegistry.StatusBarHintsId].Choice);
        Assert.Contains(SettingsRegistry.StatusBarHintsId, load.Warnings.Keys);
    }

    [Theory]
    [InlineData("{ \"commandLevel\": \"advanced+\" }", "advanced+", CommandLevel.AdvancedPlus)]
    [InlineData("{ \"commandLevel\": \"Advanced\" }", "advanced", CommandLevel.Advanced)]
    [InlineData("{ }", "everyday", CommandLevel.Everyday)]
    public void GivenACommandLevel_WhenReading_ThenTheChoiceAndTheOptionHoldIt(
        string text,
        string choice,
        CommandLevel level)
    {
        var load = Read(new ConfigSnapshot(true, text, string.Empty));

        Assert.Equal(choice, load.Values[SettingsRegistry.CommandLevelId].Choice);
        Assert.Equal(level, load.Options.CommandLevel);
    }

    [Fact]
    public void GivenABadCommandLevel_WhenReading_ThenEverydayAppliesAndTheSettingCarriesTheWarning()
    {
        var load = Read(new ConfigSnapshot(true, "{ \"commandLevel\": \"expert\" }", string.Empty));

        Assert.Equal(CommandLevel.Everyday, load.Options.CommandLevel);
        Assert.Contains(SettingsRegistry.CommandLevelId, load.Warnings.Keys);
    }

    [Fact]
    public void GivenABadLabelCapsValue_WhenReading_ThenTheSettingCarriesTheWarning()
    {
        var load = Read(new ConfigSnapshot(true, "{ \"labelCaps\": \"square\" }", string.Empty));

        Assert.Equal("rounded", load.Values[SettingsRegistry.LabelCapsId].Choice);
        Assert.Contains(SettingsRegistry.LabelCapsId, load.Warnings.Keys);
    }

    [Theory]
    [InlineData("{}", "auto", "auto")]
    [InlineData("{ \"theme\": \"dark\" }", "catppuccin-mocha", "dark")]
    [InlineData("{ \"theme\": \"Light\" }", "catppuccin-latte", "light")]
    public void GivenATheme_WhenReading_ThenTheChoiceAndTheRunningThemeHoldIt(
        string text,
        string choice,
        string expected)
    {
        var load = Read(new ConfigSnapshot(true, text, "h"));

        Assert.Equal(choice, load.Values[SettingsRegistry.ThemeId].Choice);
        Assert.Equal(expected, load.Theme.Name);
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

    [Fact]
    public void GivenABuiltInThemeName_WhenReading_ThenItIsTheChoiceWithoutWarnings()
    {
        var load = Read(new ConfigSnapshot(true, "{ \"theme\": \"Tokyo-Night-Moon\" }", "h"));

        Assert.Equal("tokyo-night-moon", load.Values[SettingsRegistry.ThemeId].Choice);
        Assert.Equal("tokyo-night-moon", load.Theme.Name);
        Assert.Empty(load.Warnings);
    }

    [Theory]
    [InlineData("{}", "none")]
    [InlineData("{ \"labelCaps\": \"angled\" }", "angled")]
    public void GivenTheClassicConsoleDefault_WhenReadingTheLabelCaps_ThenOnlyAnUnsetFileShowsNone(string text, string expected)
    {
        var load = SettingsValueReader.Read(
            new ConfigSnapshot(true, text, "h"), Home, _ => true, null, LabelCapStyle.None);

        Assert.Equal(expected, load.Values[SettingsRegistry.LabelCapsId].Choice);
    }

    [Fact]
    public void GivenAUserThemeName_WhenReadingWithTheNamesOnOffer_ThenItIsAcceptedOtherwiseWarned()
    {
        var snapshot = new ConfigSnapshot(true, "{ \"theme\": \"mine\" }", "h");

        var known = SettingsValueReader.Read(snapshot, Home, _ => true, ["mine"]);
        var unknown = Read(snapshot);

        Assert.Equal("mine", known.Theme.Name);
        Assert.Empty(known.Warnings);
        Assert.Equal("auto", unknown.Theme.Name);
        Assert.Contains(SettingsRegistry.ThemeId, unknown.Warnings.Keys);
    }

    [Fact]
    public void GivenThemeNames_WhenBuildingTheSettings_ThenTheThemeOptionsAreAutoThenTheNames()
    {
        var definitions = SettingsRegistry.WithThemes(["a", "b"]);

        var theme = definitions.Single(definition => definition.Id == SettingsRegistry.ThemeId);

        Assert.Equal(["auto", "a", "b"], theme.Choices);
        Assert.Equal(SettingsRegistry.All.Count, definitions.Count);
        Assert.Equal(
            ["auto", .. BuiltInThemes.Names],
            SettingsRegistry.All.Single(definition => definition.Id == SettingsRegistry.ThemeId).Choices);
    }

    private static SettingsLoad Read(ConfigSnapshot snapshot) =>
        SettingsValueReader.Read(snapshot, Home, path => !path.Contains("missing", StringComparison.Ordinal));
}
