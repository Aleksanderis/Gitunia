namespace Gitunia.Platform;

internal abstract class PlatformBase : IPlatform
{
    private readonly Func<string, string?> _getEnv;

    protected PlatformBase(Func<string, string?>? getEnv = null)
    {
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    public abstract string ConfigDir { get; }

    public abstract string StateDir { get; }

    public ConsoleCapabilities DetectConsole()
    {
        var interactive = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        return new ConsoleCapabilities(interactive, DetectColor(interactive), DetectUnicode());
    }

    /// <summary>User home; falls back to the temp dir so paths never become cwd-relative.</summary>
    protected static string Home
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? Path.GetTempPath() : home;
        }
    }

    protected string? Env(string name)
    {
        var value = _getEnv(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    internal ColorDepth DetectColor(bool interactive)
    {
        if (!interactive || Env("NO_COLOR") is not null)
        {
            return ColorDepth.None;
        }

        var colorTerm = Env("COLORTERM");
        if (colorTerm is "truecolor" or "24bit")
        {
            return ColorDepth.TrueColor;
        }

        var term = Env("TERM") ?? string.Empty;
        if (term.Contains("256color", StringComparison.Ordinal))
        {
            return ColorDepth.Ansi256;
        }

        // Windows Terminal sets WT_SESSION and supports VT truecolor.
        if (OperatingSystem.IsWindows() && Env("WT_SESSION") is not null)
        {
            return ColorDepth.TrueColor;
        }

        return term is "dumb" ? ColorDepth.None : ColorDepth.Ansi16;
    }

    internal bool DetectUnicode()
    {
        // Windows: assumed UTF-8 capable (Windows Terminal / modern conhost); legacy conhost may render boxes poorly.
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        var locale = Env("LC_ALL") ?? Env("LC_CTYPE") ?? Env("LANG") ?? string.Empty;
        return locale.Contains("UTF-8", StringComparison.OrdinalIgnoreCase)
            || locale.Contains("utf8", StringComparison.OrdinalIgnoreCase);
    }
}
