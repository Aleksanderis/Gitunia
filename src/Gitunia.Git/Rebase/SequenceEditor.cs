namespace Gitunia.Git.Rebase;

/// <summary>
/// The sequence-editor mode of the Gitunia executable: git calls it with the prepared todo and its own todo.
/// </summary>
public static class SequenceEditor
{
    private const int Success = 0;

    private const int UsageError = 2;

    private const int CopyFailed = 1;

    private const int ArgumentCount = 2;

    /// <summary>
    /// Copies the prepared todo (first argument) over git's todo (second). Returns the process exit code.
    /// </summary>
    public static int Copy(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != ArgumentCount)
        {
            return UsageError;
        }

        try
        {
            File.Copy(arguments[0], arguments[1], overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"gitunia: cannot write the rebase todo: {exception.Message}");
            return CopyFailed;
        }

        return Success;
    }
}
