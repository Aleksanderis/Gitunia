namespace Gitunia.Platform;

public abstract class PlatformBase : IPlatform
{
    private readonly Func<string, string?> _getEnvironmentVariable;

    protected PlatformBase(Func<string, string?>? getEnvironmentVariable = null)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
    }

    public abstract string ConfigDirectory { get; }

    public abstract string StateDirectory { get; }

    public ConsoleCapabilities DetectConsole()
    {
        var interactive = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        return new ConsoleCapabilities(interactive, DetectColor(interactive), DetectUnicode(), DetectLightBackground());
    }

    /// <summary>User home; falls back to the temp directory so paths never become cwd-relative.</summary>
    protected static string Home
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? Path.GetTempPath() : home;
        }
    }

    protected string? ReadEnvironment(string name)
    {
        var value = _getEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public ColorDepth DetectColor(bool interactive)
    {
        if (!interactive || ReadEnvironment("NO_COLOR") is not null)
        {
            return ColorDepth.None;
        }

        var colorTerm = ReadEnvironment("COLORTERM");
        if (colorTerm is "truecolor" or "24bit")
        {
            return ColorDepth.TrueColor;
        }

        var term = ReadEnvironment("TERM") ?? string.Empty;
        if (term.Contains("256color", StringComparison.Ordinal))
        {
            return ColorDepth.Ansi256;
        }

        // Windows Terminal sets WT_SESSION and supports VT truecolor.
        if (OperatingSystem.IsWindows() && ReadEnvironment("WT_SESSION") is not null)
        {
            return ColorDepth.TrueColor;
        }

        return term is "dumb" ? ColorDepth.None : ColorDepth.Ansi16;
    }

    /// <summary>
    /// True when <c>COLORFGBG</c> ("foreground;background" palette indexes, set by rxvt, Konsole and some others) names a light
    /// background (index 7 or 15). Dark and unknown both give false: the default look assumes a dark terminal.
    /// </summary>
    public bool DetectLightBackground()
    {
        var value = ReadEnvironment("COLORFGBG");
        if (value is null)
        {
            return false;
        }

        // "foreground;background" (a three-part value has the background last); a lone number is a foreground only.
        var separator = value.LastIndexOf(';');
        return separator >= 0 && value[(separator + 1)..] is "7" or "15";
    }

    public bool DetectUnicode()
    {
        // Windows: assumed UTF-8 capable (Windows Terminal / modern conhost); legacy conhost may render boxes poorly.
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        var locale = ReadEnvironment("LC_ALL") ?? ReadEnvironment("LC_CTYPE") ?? ReadEnvironment("LANG") ?? string.Empty;
        return locale.Contains("UTF-8", StringComparison.OrdinalIgnoreCase)
            || locale.Contains("utf8", StringComparison.OrdinalIgnoreCase);
    }
}
