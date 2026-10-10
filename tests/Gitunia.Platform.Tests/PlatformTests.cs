using Gitunia.Platform;
using Xunit;

namespace Gitunia.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void Dirs_are_absolute_and_end_in_app_name()
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
    public void NoColor_wins_over_truecolor() =>
        Assert.Equal(ColorDepth.None, With(("NO_COLOR", "1"), ("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void Colorterm_truecolor() =>
        Assert.Equal(ColorDepth.TrueColor, With(("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void Term_256color() =>
        Assert.Equal(ColorDepth.Ansi256, With(("TERM", "xterm-256color")).DetectColor(true));

    [Fact]
    public void Not_interactive_means_no_color() =>
        Assert.Equal(ColorDepth.None, With(("COLORTERM", "truecolor")).DetectColor(false));

    [Fact]
    public void Unicode_from_locale()
    {
        Assert.True(With(("LANG", "en_US.UTF-8")).DetectUnicode());
        Assert.False(With(("LANG", "C")).DetectUnicode());
    }

    [Fact]
    public void Linux_dirs_honour_xdg()
    {
        var p = With(("XDG_CONFIG_HOME", "/x/c"), ("XDG_STATE_HOME", "/x/s"));
        Assert.Equal(Path.Combine("/x/c", "gitunia"), p.ConfigDir);
        Assert.Equal(Path.Combine("/x/s", "gitunia"), p.StateDir);
    }
}
