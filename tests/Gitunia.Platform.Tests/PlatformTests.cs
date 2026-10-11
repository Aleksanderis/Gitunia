using Gitunia.Platform;
using Xunit;

namespace Gitunia.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void GivenCurrentPlatform_WhenReadingDirs_ThenTheyAreRootedAndNamedAfterTheApp()
    {
        var platform = PlatformInfo.Current;
        Assert.True(Path.IsPathRooted(platform.ConfigDir));
        Assert.True(Path.IsPathRooted(platform.StateDir));
        Assert.Contains(PlatformInfo.AppDirName, platform.ConfigDir);
    }
}

public class DetectionTests
{
    private static LinuxPlatform With(params (string K, string V)[] env)
    {
        var dictionary = env.ToDictionary(entry => entry.K, entry => entry.V);
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
    public void GivenXdgVars_WhenReadingLinuxDirs_ThenTheyAreUsed()
    {
        var platform = With(("XDG_CONFIG_HOME", "/x/c"), ("XDG_STATE_HOME", "/x/s"));
        Assert.Equal(Path.Combine("/x/c", "gitunia"), platform.ConfigDir);
        Assert.Equal(Path.Combine("/x/s", "gitunia"), platform.StateDir);
    }
}
