using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>Wording shared by the working-tree operations (stage, unstage, discard).</summary>
public static class OperationText
{
    /// <summary>Git's own first line of stderr; the exception message also repeats the whole command line.</summary>
    public static string Reason(Exception exception)
    {
        var text = exception is GitCommandFailedException failed ? failed.StandardError : exception.Message;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[0] : "unknown error";
    }

    public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
