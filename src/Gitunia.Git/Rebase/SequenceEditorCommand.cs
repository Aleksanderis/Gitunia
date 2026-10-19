namespace Gitunia.Git.Rebase;

/// <summary>
/// The program git runs as its sequence editor, as an argument list. Git appends the path of the todo it wrote, so the
/// program is called with the prepared todo (added by the writer) and then git's todo, and must copy the first over the
/// second (<see cref="SequenceEditor.Copy"/>). In production this is the Gitunia executable itself, so it must be the
/// published binary (or another launcher that runs the app), not a <c>dotnet</c> host that needs the assembly path.
/// </summary>
/// <param name="Arguments">The executable followed by its leading arguments.</param>
public sealed record SequenceEditorCommand(IReadOnlyList<string> Arguments)
{
    /// <summary>The first argument of the Gitunia executable when it is called as the sequence editor.</summary>
    public const string ModeArgument = "--sequence-editor";

    /// <summary>Git paths use forward slashes on every platform (git runs commands through a POSIX shell).</summary>
    public static string ToGitPath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// The value of <c>sequence.editor</c>: the command words and <paramref name="preparedTodoPath"/>, each quoted for
    /// the POSIX shell git runs it through. Git appends its own todo path.
    /// </summary>
    public string ToConfigValue(string preparedTodoPath)
    {
        var words = Arguments
            .Select(ToGitPath)
            .Append(ToGitPath(preparedTodoPath))
            .Select(RebaseTodoFormatter.ShellQuote);
        return string.Join(' ', words);
    }
}
