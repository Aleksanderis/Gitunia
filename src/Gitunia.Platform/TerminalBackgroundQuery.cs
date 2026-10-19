using System.Globalization;

namespace Gitunia.Platform;

/// <summary>
/// Asks the terminal for its background colour with OSC 11 (<c>ESC ] 11 ; ? ST</c>) and reports whether it is light.
/// Terminals that support it answer within a few milliseconds; the rest are given a short timeout, so only they pay it.
/// </summary>
public static class TerminalBackgroundQuery
{
    /// <summary>How long a silent terminal is waited for.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Queries the terminal on the console. Returns null when the answer is unknown: not interactive, Windows (no
    /// reliable answer from conhost, so the <c>COLORFGBG</c> fallback decides), <c>TERM</c> unset, <c>dumb</c> or
    /// <c>linux</c>, no answer in time, or an answer that cannot be parsed.
    /// </summary>
    /// <param name="interactive">Input and output are a terminal.</param>
    /// <param name="term">The <c>TERM</c> value, null when unset.</param>
    /// <param name="timeout">How long to wait for the answer.</param>
    public static bool? Query(bool interactive, string? term, TimeSpan timeout)
    {
        if (!interactive || OperatingSystem.IsWindows() || term is null or "dumb" or "linux")
        {
            return null;
        }

        try
        {
            // The DA1 query behind it (ESC [ c) is answered by every terminal, in order: its reply ends the wait.
            Console.Out.Write("\u001b]11;?\u001b\\\u001b[c");
            Console.Out.Flush();
            var reader = new TerminalAnswerReader(() => Console.KeyAvailable, () => Console.ReadKey(intercept: true).KeyChar);
            var response = reader.Read(timeout);
            return response is null ? null : ParseIsLight(response);
        }
        catch (Exception)
        {
            // A best-effort probe: whatever goes wrong, startup continues with the COLORFGBG fallback.
            return null;
        }
    }

    /// <summary>
    /// Reads a colour from an OSC 11 answer (<c>rgb:RRRR/GGGG/BBBB</c> with 1 to 4 hex digits per channel, optionally
    /// <c>rgba:</c>) and says whether it is light (relative luminance above one half). Null when the text holds no
    /// colour.
    /// </summary>
    /// <param name="response">The answer text, with or without its escape framing.</param>
    public static bool? ParseIsLight(string response)
    {
        var start = response.IndexOf("rgb", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var colon = response.IndexOf(':', start);
        if (colon < 0)
        {
            return null;
        }

        var body = response[(colon + 1)..];
        var end = body.IndexOfAny(['\u0007', '\u001b', '\\']);
        if (end >= 0)
        {
            body = body[..end];
        }

        var parts = body.Split('/');
        if (parts.Length is < 3 or > 4)
        {
            return null;
        }

        var channels = new double[3];
        for (var i = 0; i < 3; i++)
        {
            if (parts[i].Length is < 1 or > 4
                || !int.TryParse(parts[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            channels[i] = value / (Math.Pow(16, parts[i].Length) - 1);
        }

        // A plain weighted luminance of the encoded channels: a heuristic for "light or dark", not a colour-science value.
        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]) > 0.5;
    }
}
