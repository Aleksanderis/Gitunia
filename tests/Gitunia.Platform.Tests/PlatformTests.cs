using Gitunia.Platform;
using Xunit;

namespace Gitunia.Platform.Tests;

public class PlatformTests
{
    [Fact]
    public void Given_current_platform_When_reading_dirs_Then_they_are_rooted_and_named_after_the_app()
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
    public void Given_NO_COLOR_and_truecolor_When_detecting_color_Then_none() =>
        Assert.Equal(ColorDepth.None, With(("NO_COLOR", "1"), ("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void Given_COLORTERM_truecolor_When_detecting_color_Then_truecolor() =>
        Assert.Equal(ColorDepth.TrueColor, With(("COLORTERM", "truecolor")).DetectColor(true));

    [Fact]
    public void Given_TERM_256color_When_detecting_color_Then_ansi256() =>
        Assert.Equal(ColorDepth.Ansi256, With(("TERM", "xterm-256color")).DetectColor(true));

    [Fact]
    public void Given_non_interactive_console_When_detecting_color_Then_none() =>
        Assert.Equal(ColorDepth.None, With(("COLORTERM", "truecolor")).DetectColor(false));

    [Fact]
    public void Given_locale_When_detecting_unicode_Then_utf8_locales_are_supported()
    {
        Assert.True(With(("LANG", "en_US.UTF-8")).DetectUnicode());
        Assert.False(With(("LANG", "C")).DetectUnicode());
    }

    [Fact]
    public void Given_XDG_vars_When_reading_linux_dirs_Then_they_are_used()
    {
        var p = With(("XDG_CONFIG_HOME", "/x/c"), ("XDG_STATE_HOME", "/x/s"));
        Assert.Equal(Path.Combine("/x/c", "gitunia"), p.ConfigDir);
        Assert.Equal(Path.Combine("/x/s", "gitunia"), p.StateDir);
    }
}
