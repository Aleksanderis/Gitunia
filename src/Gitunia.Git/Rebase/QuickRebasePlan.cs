namespace Gitunia.Git.Rebase;

/// <summary>
/// What a quick rebase will do, read before the confirmation so the dialog can name it. Nothing was changed yet.
/// </summary>
/// <param name="Kind">The edit.</param>
/// <param name="Commit">The full hash of the commit the edit acts on.</param>
/// <param name="Message">The full message of that commit.</param>
/// <param name="Base">Where the rebase starts: the commit before the edited one (two before for a fixup).</param>
/// <param name="Range">The commits the rebase replays, oldest first; the edited commit is one of them.</param>
/// <param name="Head">HEAD when the plan was made; a run on a different HEAD is refused.</param>
/// <param name="IntoCommit">The commit a fixup melds into (the parent of <see cref="Commit"/>); null otherwise.</param>
public sealed record QuickRebasePlan(
    QuickRebaseKind Kind,
    string Commit,
    string Message,
    RebaseBase Base,
    IReadOnlyList<RebaseTodoCommit> Range,
    string Head,
    RebaseTodoCommit? IntoCommit)
{
    /// <summary>The first line of the message of <see cref="Commit"/>.</summary>
    public string Subject => FirstLine(Message);

    /// <summary>How many commits after the edited one are replayed and so get new hashes.</summary>
    public int LaterCommits => Range.Count - 1 - IndexOfCommit();

    /// <summary>The position of the edited commit in <see cref="Range"/>.</summary>
    public int IndexOfCommit()
    {
        for (var index = 0; index < Range.Count; index++)
        {
            if (string.Equals(Range[index].Hash, Commit, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The first line of a message.</summary>
    public static string FirstLine(string message)
    {
        var end = message.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? message : message[..end]).TrimEnd('\r');
    }
}
