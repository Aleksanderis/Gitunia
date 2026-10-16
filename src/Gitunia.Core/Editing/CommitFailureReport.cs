namespace Gitunia.Core.Editing;

/// <summary>
/// What the output overlay shows when <c>git commit</c> fails: git's and the hooks' own output, then a hint. The commit runs
/// without a terminal (stdin is closed), so a hook or a signing program that asks a question fails instead of waiting.
/// </summary>
public static class CommitFailureReport
{
    /// <summary>Heading of the output section.</summary>
    public const string Heading = "Output of git commit";

    /// <summary>Title of the overlay.</summary>
    public const string Title = "Commit failed";

    /// <summary>Shown when git printed nothing.</summary>
    public const string NoOutput = "(git printed no output)";

    /// <summary>The hint under the output.</summary>
    public const string Hint =
        "No keyboard input reaches git or its hooks here: a hook that asks a question or a signing program that needs a passphrase fails. "
        + "Your message is kept. Press <space>te in a file list to run git commit in the terminal (it can prompt), or fix the cause and commit again.";

    /// <summary>Longest status bar notice.</summary>
    private const int MaximumNoticeLength = 160;

    /// <summary>The lines of the overlay: the output as printed, a blank line, then the hint.</summary>
    /// <param name="standardError">What git and the hooks wrote to stderr.</param>
    /// <param name="standardOutput">What git printed to stdout (e.g. "nothing to commit"), shown after stderr.</param>
    public static IReadOnlyList<string> Lines(string standardError, string standardOutput = "")
    {
        var output = OutputLines(Combine(standardError, standardOutput));
        return [.. output.Count == 0 ? [NoOutput] : output, string.Empty, Hint];
    }

    /// <summary>The first line of the output, for the status bar ("Commit failed: ...").</summary>
    /// <param name="standardError">What git and the hooks wrote to stderr.</param>
    /// <param name="standardOutput">What git printed to stdout.</param>
    public static string Notice(string standardError, string standardOutput = "")
    {
        var first = OutputLines(Combine(standardError, standardOutput)).FirstOrDefault(line => line.Trim().Length > 0) ?? NoOutput;
        var text = first.Trim();
        return text.Length > MaximumNoticeLength ? $"{text[..MaximumNoticeLength]}..." : text;
    }

    private static string Combine(string standardError, string standardOutput)
    {
        if (standardOutput.Trim().Length == 0)
        {
            return standardError;
        }

        return standardError.Trim().Length == 0 ? standardOutput : $"{standardError.TrimEnd()}\n{standardOutput}";
    }

    private static List<string> OutputLines(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }
}
