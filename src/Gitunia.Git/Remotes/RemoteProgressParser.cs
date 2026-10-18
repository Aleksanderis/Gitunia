using System.Globalization;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Git.Remotes;

/// <summary>Parses one stderr segment of <c>--progress</c> output (<c>Receiving objects:  45% (9/20)</c>).</summary>
public static class RemoteProgressParser
{
    private const string RemotePrefix = "remote:";
    private const char StageSeparator = ':';
    private const char PercentSign = '%';
    private const char Space = ' ';
    private const char CountSeparator = ',';
    private const int MaximumPercent = 100;

    /// <summary>The progress in the segment, or null when it is any other line (<c>From ...</c>, ref updates, errors).</summary>
    public static RemoteProgress? Parse(string segment)
    {
        var text = segment.Trim();
        if (text.StartsWith(RemotePrefix, StringComparison.Ordinal))
        {
            text = text[RemotePrefix.Length..].TrimStart();
        }

        var separator = text.IndexOf(StageSeparator, StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        var stage = text[..separator];
        if (!stage.All(character => char.IsAsciiLetter(character) || character == Space))
        {
            return null;
        }

        var rest = text[(separator + 1)..].TrimStart();
        var digits = 0;
        while (digits < rest.Length && char.IsAsciiDigit(rest[digits]))
        {
            digits++;
        }

        if (digits == 0)
        {
            return null;
        }

        if (digits < rest.Length
            && rest[digits] == PercentSign
            && int.TryParse(rest[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out var percent))
        {
            return new RemoteProgress(stage, Math.Min(percent, MaximumPercent));
        }

        // A count-only stage reads "Enumerating objects: 5, done."; any other text after the number is not progress.
        return digits == rest.Length || rest[digits] == CountSeparator ? new RemoteProgress(stage, null) : null;
    }
}
