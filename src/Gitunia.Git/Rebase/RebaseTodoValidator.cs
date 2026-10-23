namespace Gitunia.Git.Rebase;

/// <summary>Checks a todo against the commits of its range before git is started.</summary>
public static class RebaseTodoValidator
{
    private const char CommentMark = '#';

    private const string MeldWithoutBase = "The first commit that is kept cannot be a squash or a fixup";

    private const string MeldAfterReset =
        "A squash or a fixup cannot follow a reset: there is no commit on its line to meld into";

    /// <summary>
    /// Throws <see cref="RebaseRangeException"/> unless <paramref name="entries"/> name every commit of
    /// <paramref name="range"/> exactly once (full hashes), keep every merge, label and reset line of it, the first
    /// entry that is not a drop is a pick or a reword, every reword has a message and every label is defined before
    /// a reset or merge uses it (<see cref="FindProblem"/>).
    /// </summary>
    public static void Validate(IReadOnlyList<RebaseTodoCommit> range, IReadOnlyList<RebaseTodoEntry> entries)
    {
        var expected = new Dictionary<string, RebaseTodoAction>(StringComparer.Ordinal);
        var structure = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var commit in range)
        {
            if (commit.Action.IsStructure())
            {
                var key = StructureKey(commit.Action, commit.Hash);
                structure[key] = structure.GetValueOrDefault(key) + 1;
            }
            else
            {
                expected[commit.Hash] = commit.Action;
            }
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Action.IsStructure())
            {
                var key = StructureKey(entry.Action, entry.Commit);
                var left = structure.GetValueOrDefault(key);
                if (left == 0)
                {
                    throw new RebaseRangeException($"The todo has a '{key}' line the range does not");
                }

                structure[key] = left - 1;
                continue;
            }

            if (!expected.TryGetValue(entry.Commit, out var kind))
            {
                throw new RebaseRangeException($"{entry.Commit} is not a commit of the range being rebased");
            }

            if (!seen.Add(entry.Commit))
            {
                throw new RebaseRangeException($"{entry.Commit} appears twice in the todo");
            }

            if (kind == RebaseTodoAction.Merge && entry.Action != RebaseTodoAction.Merge)
            {
                throw new RebaseRangeException($"{entry.Commit} is a merge commit and can only be kept as it is");
            }

            if (kind != RebaseTodoAction.Merge && entry.Action == RebaseTodoAction.Merge)
            {
                throw new RebaseRangeException($"{entry.Commit} is not a merge commit");
            }

            if (entry.Action == RebaseTodoAction.Reword && string.IsNullOrWhiteSpace(entry.Message))
            {
                throw new RebaseRangeException($"The new message of {entry.Commit} is empty");
            }
        }

        if (seen.Count != expected.Count)
        {
            throw new RebaseRangeException(
                $"The todo lists {seen.Count} of the {expected.Count} commits; drop a commit explicitly to remove it");
        }

        if (structure.Values.Any(left => left > 0))
        {
            throw new RebaseRangeException("The todo lost a label or reset line; they cannot be removed");
        }

        if (FindProblem(entries) is { } problem)
        {
            throw new RebaseRangeException(problem);
        }
    }

    /// <summary>
    /// What makes the order of <paramref name="entries"/> unusable, null when it is fine: a squash or fixup with no
    /// commit before it on its line (at the start or right after a reset), or a label used by a reset or merge before
    /// its line. Names no label defines (commits of the original history) are left to git.
    /// </summary>
    public static string? FindProblem(IReadOnlyList<RebaseTodoEntry> entries)
    {
        var labels = entries.Where(entry => entry.Action == RebaseTodoAction.Label)
            .Select(entry => entry.Commit)
            .ToHashSet(StringComparer.Ordinal);
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var hasBase = false;
        var afterReset = false;
        foreach (var entry in entries)
        {
            switch (entry.Action)
            {
                case RebaseTodoAction.Label:
                    defined.Add(entry.Commit);
                    break;
                case RebaseTodoAction.Reset:
                    if (labels.Contains(entry.Commit) && !defined.Contains(entry.Commit))
                    {
                        return LabelOrder(entry.Commit);
                    }

                    hasBase = false;
                    afterReset = true;
                    break;
                case RebaseTodoAction.Merge:
                    foreach (var name in MergeLabels(entry.Argument))
                    {
                        if (labels.Contains(name) && !defined.Contains(name))
                        {
                            return LabelOrder(name);
                        }
                    }

                    hasBase = true;
                    break;
                case RebaseTodoAction.Pick or RebaseTodoAction.Reword:
                    hasBase = true;
                    break;
                case RebaseTodoAction.Squash or RebaseTodoAction.Fixup when !hasBase:
                    return afterReset ? MeldAfterReset : MeldWithoutBase;
            }
        }

        return null;
    }

    /// <summary>The labels a <c>merge</c> line merges: the words of its argument before the <c>#</c> oneline.</summary>
    public static IEnumerable<string> MergeLabels(string? argument)
    {
        foreach (var word in (argument ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word[0] == CommentMark)
            {
                yield break;
            }

            yield return word;
        }
    }

    private static string LabelOrder(string name) => $"'{name}' is used before the line that labels it";

    private static string StructureKey(RebaseTodoAction action, string name) =>
        action == RebaseTodoAction.Label ? $"label {name}" : $"reset {name}";
}
