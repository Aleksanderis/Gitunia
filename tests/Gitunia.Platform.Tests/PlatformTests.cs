using Gitunia.Platform;
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
    private static LinuxPlatform With(params (string Key, string Value)[] environment)
    {
        var dictionary = environment.ToDictionary(entry => entry.Key, entry => entry.Value);
        return new LinuxPlatform(key => dictionary.GetValueOrDefault(key));
    }

    [Fact]
    public void GivenNoColorAndTruecolor_WhenDetectingColor_ThenNone() =>
        Assert.Equal(ColorDepth.None, With(("NO_COLOR", "1"), ("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void GivenColortermTruecolor_WhenDetectingColor_ThenTruecolor() =>
        Assert.Equal(ColorDepth.TrueColor, With(("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void GivenTerm256color_WhenDetectingColor_ThenAnsi256() =>
        Assert.Equal(ColorDepth.Ansi256, With(("TERM", "xterm-256color")).DetectColor(true));

    [Fact]
    public void GivenNonInteractiveConsole_WhenDetectingColor_ThenNone() =>
        Assert.Equal(ColorDepth.None, With(("COLORTERM", "truecolor")).DetectColor(false));

    [Fact]
    public void GivenLocale_WhenDetectingUnicode_ThenUtf8LocalesAreSupported()
    {
        Assert.True(With(("LANG", "en_US.UTF-8")).DetectUnicode());
        Assert.False(With(("LANG", "C")).DetectUnicode());
    }

    [Fact]
    public void GivenXdgVars_WhenReadingLinuxDirectories_ThenTheyAreUsed()
    {
        var platform = With(("XDG_CONFIG_HOME", "/x/c"), ("XDG_STATE_HOME", "/x/s"));
        Assert.Equal(Path.Combine("/x/c", "gitunia"), platform.ConfigDirectory);
        Assert.Equal(Path.Combine("/x/s", "gitunia"), platform.StateDirectory);
    }
}
