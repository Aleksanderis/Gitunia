namespace Gitunia.Core.Clipboard;

/// <summary>Chooses <see cref="Osc52Options"/> from environment variables. The lookup is injected; the process environment is never read here.</summary>
public static class Osc52Environment
{
    /// <summary>The base64 payload limit that xterm and most terminals accept.</summary>
    public const int DefaultMaximumEncodedLength = 100_000;

    /// <summary>Detect the delivery mode.</summary>
    /// <param name="lookup">Returns the value of an environment variable, or null when it is unset.</param>
    public static Osc52Options Detect(Func<string, string?> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var terminal = lookup("TERM");
        if (string.Equals(terminal, "dumb", StringComparison.Ordinal) || string.Equals(terminal, "linux", StringComparison.Ordinal))
        {
            return new Osc52Options(Osc52Mode.Unsupported, DefaultMaximumEncodedLength, $"the terminal type '{terminal}' has no clipboard support");
        }

        if (!string.IsNullOrEmpty(lookup("TMUX")))
        {
            return new Osc52Options(Osc52Mode.Tmux, DefaultMaximumEncodedLength);
        }

        if (!string.IsNullOrEmpty(lookup("STY")))
        {
            return new Osc52Options(Osc52Mode.Screen, DefaultMaximumEncodedLength);
        }

        return new Osc52Options(Osc52Mode.Direct, DefaultMaximumEncodedLength);
    }
}
