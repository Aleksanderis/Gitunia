namespace Gitunia.Platform;

internal abstract class PlatformBase : IPlatform
{
    public abstract string ConfigDir { get; }

    public abstract string StateDir { get; }

    public ConsoleCapabilities DetectConsole()
    {
        var interactive = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        return new ConsoleCapabilities(interactive, DetectColor(interactive), DetectUnicode());
    }

    protected static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    protected static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static ColorDepth DetectColor(bool interactive)
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

        // Windows Terminal and modern conhost support VT truecolor; WT_SESSION marks Windows Terminal.
        if (OperatingSystem.IsWindows() && Env("WT_SESSION") is not null)
        {
            return ColorDepth.TrueColor;
        }

        return term is "dumb" ? ColorDepth.None : ColorDepth.Ansi16;
    }

    private static bool DetectUnicode()
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        var locale = Env("LC_ALL") ?? Env("LC_CTYPE") ?? Env("LANG") ?? string.Empty;
        return locale.Contains("UTF-8", StringComparison.OrdinalIgnoreCase)
            || locale.Contains("utf8", StringComparison.OrdinalIgnoreCase);
    }
}
