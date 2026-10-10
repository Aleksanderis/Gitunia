using Gitunia.Platform;
using Xunit;

namespace Gitunia.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void GivenCurrentPlatform_WhenReadingDirs_ThenTheyAreRootedAndNamedAfterTheApp()
    {
        var p = PlatformInfo.Current;
        Assert.True(Path.IsPathRooted(p.ConfigDir));
        Assert.True(Path.IsPathRooted(p.StateDir));
        Assert.Contains(PlatformInfo.AppDirName, p.ConfigDir);
    }
}

public class DetectionTests
{
    private static LinuxPlatform With(params (string K, string V)[] env)
    {
        var d = env.ToDictionary(e => e.K, e => e.V);
        return new LinuxPlatform(k => d.GetValueOrDefault(k));
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
        var p = With(("XDG_CONFIG_HOME", "/x/c"), ("XDG_STATE_HOME", "/x/s"));
        Assert.Equal(Path.Combine("/x/c", "gitunia"), p.ConfigDir);
        Assert.Equal(Path.Combine("/x/s", "gitunia"), p.StateDir);
    }
}
