namespace Gitunia.Git.Rebase;

/// <summary>
/// The program git runs as its sequence editor, as an argument list. Git appends the path of the todo it wrote, so the
/// program is called with the prepared todo (added by the writer) and then git's todo, and must copy the first over the
/// second (<see cref="SequenceEditor.Copy"/>).
/// </summary>
/// <param name="Arguments">The executable followed by its leading arguments.</param>
public sealed record SequenceEditorCommand(IReadOnlyList<string> Arguments)
{
    /// <summary>The first argument of the Gitunia executable when it is called as the sequence editor.</summary>
    public const string ModeArgument = "--sequence-editor";
}
