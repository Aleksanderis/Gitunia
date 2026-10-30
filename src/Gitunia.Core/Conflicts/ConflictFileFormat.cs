namespace Gitunia.Core.Conflicts;

/// <summary>How the conflicted file is written, kept so the saved result reads like the file did.</summary>
/// <param name="LineEnding">The line ending written between result lines: CRLF when the file used it more than LF.</param>
/// <param name="HasFinalNewline">Whether the file ended with a line break.</param>
/// <param name="HadMixedLineEndings">
/// The file mixed CRLF and LF lines; saving writes <paramref name="LineEnding"/> everywhere.
/// </param>
public sealed record ConflictFileFormat(string LineEnding, bool HasFinalNewline, bool HadMixedLineEndings)
{
    private const string LineFeed = "\n";
    private const string CarriageReturnLineFeed = "\r\n";

    /// <summary>Reads the format off the file's text.</summary>
    public static ConflictFileFormat Of(string text)
    {
        var windows = CountOf(text, CarriageReturnLineFeed);
        var unix = CountOf(text, LineFeed) - windows;

        return new ConflictFileFormat(
            windows > unix ? CarriageReturnLineFeed : LineFeed,
            text.EndsWith(LineFeed, StringComparison.Ordinal),
            windows > 0 && unix > 0);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var position = text.IndexOf(value, StringComparison.Ordinal);
        while (position >= 0)
        {
            count++;
            position = text.IndexOf(value, position + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
