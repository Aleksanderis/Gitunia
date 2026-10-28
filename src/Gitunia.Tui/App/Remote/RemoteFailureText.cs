using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// The one line that tells why a fetch, pull or push failed. Git's stderr holds progress lines (ending in a carriage
/// return) and a <c>To &lt;url&gt;</c> line before the reason, so the first line is rarely the useful one.
/// </summary>
public static class RemoteFailureText
{
    private static readonly string[] ReasonPrefixes = ["fatal:", "error:", "! ["];

    /// <summary>
    /// Whether a push failed because the remote branch has commits the local one lacks while the local one has others
    /// (git: <c>! [rejected] ... (non-fast-forward)</c>), the case a force push with a lease can answer. A "fetch
    /// first" rejection is not: our remote-tracking branch is stale, so a lease would be refused as well.
    /// </summary>
    public static bool IsNonFastForward(Exception exception) =>
        exception is GitCommandFailedException failed
        && failed.StandardError.Contains("[rejected]", StringComparison.Ordinal)
        && failed.StandardError.Contains("(non-fast-forward)", StringComparison.Ordinal);

    public static string Reason(Exception exception)
    {
        var text = exception is GitCommandFailedException failed ? failed.StandardError : exception.Message;
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var reason = lines.FirstOrDefault(
            line => ReasonPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal)));

        return reason ?? (lines.Length > 0 ? lines[^1] : "unknown error");
    }
}
