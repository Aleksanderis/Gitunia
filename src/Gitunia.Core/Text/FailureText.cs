using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Core.Text;

/// <summary>Wording for a failed operation, shared by the effect handlers and the controllers.</summary>
public static class FailureText
{
    /// <summary>The first line of the process's stderr; the exception message also repeats the whole command line.</summary>
    public static string Reason(Exception exception)
    {
        var text = exception is GitCommandFailedException failed ? failed.StandardError : exception.Message;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[0] : "unknown error";
    }
}
