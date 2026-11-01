using Gitunia.Core.Text;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>Wording shared by the working-tree operations (stage, unstage, discard).</summary>
public static class OperationText
{
    /// <summary>The first line of git's own stderr; the exception message also repeats the whole command line.</summary>
    public static string Reason(Exception exception) => FailureText.Reason(exception);

    public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
