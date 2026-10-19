namespace Gitunia.Git.Rebase;

/// <summary>Checks a todo against the commits of its range before git is started.</summary>
public static class RebaseTodoValidator
{
    /// <summary>
    /// Throws <see cref="RebaseRangeException"/> unless <paramref name="entries"/> name every commit of
    /// <paramref name="range"/> exactly once (full hashes), the first entry that is not a drop is a pick or a reword,
    /// and every reword has a message.
    /// </summary>
    public static void Validate(IReadOnlyList<RebaseTodoCommit> range, IReadOnlyList<RebaseTodoEntry> entries)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var commit in range)
        {
            expected.Add(commit.Hash);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var meldWithoutBase = false;
        var hasBase = false;
        foreach (var entry in entries)
        {
            if (!expected.Contains(entry.Commit))
            {
                throw new RebaseRangeException($"{entry.Commit} is not a commit of the range being rebased");
            }

            if (!seen.Add(entry.Commit))
            {
                throw new RebaseRangeException($"{entry.Commit} appears twice in the todo");
            }

            if (entry.Action == RebaseTodoAction.Reword && string.IsNullOrWhiteSpace(entry.Message))
            {
                throw new RebaseRangeException($"The new message of {entry.Commit} is empty");
            }

            hasBase |= entry.Action is RebaseTodoAction.Pick or RebaseTodoAction.Reword;
            meldWithoutBase |= !hasBase && entry.Action is RebaseTodoAction.Squash or RebaseTodoAction.Fixup;
        }

        if (seen.Count != expected.Count)
        {
            throw new RebaseRangeException(
                $"The todo lists {seen.Count} of the {expected.Count} commits; drop a commit explicitly to remove it");
        }

        if (meldWithoutBase)
        {
            throw new RebaseRangeException("The first commit that is kept cannot be a squash or a fixup");
        }
    }
}
