namespace Gitunia.Platform;

/// <summary>
/// The one <see cref="IPlatform"/> for every OS. Gitunia is a terminal app, so it follows terminal-app convention instead of
/// per-OS directories: <c>~/.config/gitunia</c> and <c>~/.local/state/gitunia</c> (or <c>XDG_CONFIG_HOME</c> / <c>XDG_STATE_HOME</c>
/// when set) on Linux, macOS and Windows alike, so the config folder can be kept in a dotfiles repository.
/// </summary>
public sealed class TerminalPlatform(Func<string, string?>? getEnvironmentVariable = null, Func<bool>? isWindows = null) : IPlatform
{
    private readonly Func<bool> _isWindows = isWindows ?? OperatingSystem.IsWindows;
    private readonly Func<string, string?> _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;

    public string ConfigDirectory => Path.Combine(ReadEnvironment("XDG_CONFIG_HOME") ?? Path.Combine(Home, ".config"), PlatformInfo.AppDirectoryName);

    public string StateDirectory => Path.Combine(ReadEnvironment("XDG_STATE_HOME") ?? Path.Combine(Home, ".local", "state"), PlatformInfo.AppDirectoryName);

    public ConsoleCapabilities DetectConsole()
    {
        var interactive = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        return new ConsoleCapabilities(interactive, DetectColor(interactive), DetectUnicode(interactive), DetectLightBackground(), DetectClassicConsole());
    }

    public bool? QueryLightBackground(ConsoleCapabilities capabilities)
    {
        if (capabilities.Color == ColorDepth.None)
        {
            return null;
        }

        return TerminalBackgroundQuery.Query(
            capabilities.IsInteractive,
            ReadEnvironment("TERM"),
            TerminalBackgroundQuery.DefaultTimeout);
    }

    /// <summary>User home; falls back to the temp directory so paths never become cwd-relative.</summary>
    private static string Home
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? Path.GetTempPath() : home;
        }
    }

    private string? ReadEnvironment(string name)
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
        if (_isWindows() && ReadEnvironment("WT_SESSION") is not null)
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

    /// <summary>
    /// True on Windows when <c>WT_SESSION</c> is not set (Windows Terminal sets it): the classic console host, whose font
    /// carries no icon glyphs. Other Windows terminals without that variable (VS Code, mintty) count as classic too, a
    /// known trade-off: an explicit <c>labelCaps</c> restores the caps there. Linux and macOS terminals are never classic: there is no such host, and a
    /// terminal's font cannot be asked, so they keep the full look.
    /// </summary>
    public bool DetectClassicConsole() => _isWindows() && ReadEnvironment("WT_SESSION") is null;

    /// <param name="interactive">True when attached to a terminal. On Windows only then is UTF-8 assumed: redirected output
    /// and non-UI prints go through the legacy OEM code page (437, 850), where box-drawing characters turn into <c>?</c>.</param>
    public bool DetectUnicode(bool interactive)
    {
        // Windows: assumed UTF-8 capable (Windows Terminal / modern conhost); legacy conhost may render boxes poorly.
        if (_isWindows())
        {
            return interactive;
        }

        var locale = ReadEnvironment("LC_ALL") ?? ReadEnvironment("LC_CTYPE") ?? ReadEnvironment("LANG") ?? string.Empty;
        return locale.Contains("UTF-8", StringComparison.OrdinalIgnoreCase)
            || locale.Contains("utf8", StringComparison.OrdinalIgnoreCase);
    }
}
