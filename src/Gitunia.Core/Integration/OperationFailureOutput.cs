using Gitunia.Core.Operations;
using Gitunia.Git.Operations.Models;

namespace Gitunia.Core.Integration;

/// <summary>
/// The output box of a failed continue, abort or skip: everything git (and its hooks) printed, so a message the status
/// line cuts off can still be read. Pure.
/// </summary>
public static class OperationFailureOutput
{
    /// <summary>Shown when git printed nothing.</summary>
    public const string NoOutput = "(git printed no output)";

    /// <summary>The box for one failed call.</summary>
    /// <param name="verb">The action: continue, abort or skip.</param>
    /// <param name="info">The operation in progress.</param>
    /// <param name="standardError">What git wrote to stderr.</param>
    /// <param name="standardOutput">What git printed to stdout, shown after stderr.</param>
    public static FailureOutput Of(string verb, OperationInfo info, string standardError, string standardOutput)
    {
        var name = OperationBannerBuilder.NameOf(info.Kind);
        var lines = Lines(standardError).Concat(Lines(standardOutput)).ToList();

        return new FailureOutput(
            $"Could not {verb} the {name}",
            $"Output of git {verb}",
            lines.Count == 0 ? [NoOutput] : lines);
    }

    private static List<string> Lines(string text)
    {
        var split = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var lines = split.Select(line => line.TrimEnd('\r')).ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        while (lines.Count > 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }

        return lines;
    }
}
