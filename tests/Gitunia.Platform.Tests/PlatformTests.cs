using Xunit;

namespace Gitunia.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void GivenCurrentPlatform_WhenReadingDirectories_ThenTheyAreRootedAndNamedAfterTheApp()
    {
        var platform = PlatformInfo.Current;
        Assert.True(Path.IsPathRooted(platform.ConfigDirectory));
        Assert.True(Path.IsPathRooted(platform.StateDirectory));
        Assert.Contains(PlatformInfo.AppDirectoryName, platform.ConfigDirectory);
    }
}

public class DetectionTests
{
    [Fact]
    public void GivenNoColorAndTruecolor_WhenDetectingColor_ThenNone() =>
        Assert.Equal(ColorDepth.None, With(new EnvironmentVariable("NO_COLOR", "1"), new EnvironmentVariable("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void GivenColortermTruecolor_WhenDetectingColor_ThenTruecolor() =>
        Assert.Equal(ColorDepth.TrueColor, With(new EnvironmentVariable("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void GivenTerm256color_WhenDetectingColor_ThenAnsi256() =>
        Assert.Equal(ColorDepth.Ansi256, With(new EnvironmentVariable("TERM", "xterm-256color")).DetectColor(true));

    [Fact]
    public void GivenNonInteractiveConsole_WhenDetectingColor_ThenNone() =>
        Assert.Equal(ColorDepth.None, With(new EnvironmentVariable("COLORTERM", "truecolor")).DetectColor(false));

    [Theory]
    [InlineData("15;0", false)]
    [InlineData("0;15", true)]
    [InlineData("0;default;15", true)]
    [InlineData("12;7", true)]
    [InlineData("7;0", false)]
    [InlineData("garbage", false)]
    [InlineData("15", false)]
    [InlineData("7", false)]
    public void GivenColorFgBg_WhenDetectingBackground_ThenOnlyLightBackgroundsAreLight(string value, bool expected) =>
        Assert.Equal(expected, With(new EnvironmentVariable("COLORFGBG", value)).DetectLightBackground());

    [Fact]
    public void GivenNoColorFgBg_WhenDetectingBackground_ThenDark() =>
        Assert.False(With().DetectLightBackground());

    [Fact]
    public void GivenLocale_WhenDetectingUnicode_ThenUtf8LocalesAreSupported()
    {
        Assert.True(With(new EnvironmentVariable("LANG", "en_US.UTF-8")).DetectUnicode());
        Assert.False(With(new EnvironmentVariable("LANG", "C")).DetectUnicode());
    }

    [Fact]
    public void GivenXdgVars_WhenReadingDirectories_ThenTheyAreUsed()
    {
        var platform = With(new EnvironmentVariable("XDG_CONFIG_HOME", "/x/c"), new EnvironmentVariable("XDG_STATE_HOME", "/x/s"));
        Assert.Equal(Path.Combine("/x/c", "gitunia"), platform.ConfigDirectory);
        Assert.Equal(Path.Combine("/x/s", "gitunia"), platform.StateDirectory);
    }

    [Fact]
    public void GivenNoXdgVars_WhenReadingDirectories_ThenTheyAreUnderTheHomeDotDirectories()
    {
        var platform = With();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, ".config", "gitunia"), platform.ConfigDirectory);
        Assert.Equal(Path.Combine(home, ".local", "state", "gitunia"), platform.StateDirectory);
    }

    private static TerminalPlatform With(params EnvironmentVariable[] environment)
    {
        var dictionary = environment.ToDictionary(entry => entry.Key, entry => entry.Value);
        return new TerminalPlatform(key => dictionary.GetValueOrDefault(key));
    }
}
