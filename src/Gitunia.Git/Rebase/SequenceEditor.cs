namespace Gitunia.Git.Rebase;

/// <summary>
/// The sequence-editor mode of the Gitunia executable: git calls it with the prepared todo and its own todo.
/// </summary>
public static class SequenceEditor
{
    private const int Success = 0;

    private const int UsageError = 2;

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

        File.Copy(arguments[0], arguments[1], overwrite: true);
        return Success;
    }
}
