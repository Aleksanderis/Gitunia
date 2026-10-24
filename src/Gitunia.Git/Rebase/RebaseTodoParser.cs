namespace Gitunia.Git.Rebase;

/// <summary>
/// Reads the todo git prepares for <c>rebase --rebase-merges</c> (hashes spelled out, long command words): the
/// <c>pick</c>, <c>label</c>, <c>reset</c> and <c>merge -C</c> lines. Blank lines and comments are skipped. Pure.
/// </summary>
public static class RebaseTodoParser
{
    private const string OnelineMark = "# ";

    private const string NewRoot = "[new root]";

    private const string MergeFlag = "-C";

    /// <summary>
    /// Throws <see cref="RebaseRangeException"/> for a line it does not know, so a command the editor cannot carry
    /// (<c>exec</c>, <c>update-ref</c>) is never dropped silently.
    /// </summary>
    public static List<RebaseTodoCommit> Parse(string todo)
    {
        var lines = new List<RebaseTodoCommit>();
        foreach (var raw in todo.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            lines.Add(ParseLine(line));
        }

        return lines;
    }

    private static RebaseTodoCommit ParseLine(string line)
    {
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        var word = space < 0 ? line : line[..space];
        var rest = space < 0 ? string.Empty : line[(space + 1)..].Trim();
        switch (word)
        {
            case "label" when rest.Length > 0:
                return new RebaseTodoCommit(rest, string.Empty, RebaseTodoAction.Label);
            case "reset" when rest.Length > 0:
                return new RebaseTodoCommit(ResetName(rest), string.Empty, RebaseTodoAction.Reset);
            case "pick" when rest.Length > 0:
                var hashEnd = rest.IndexOf(' ', StringComparison.Ordinal);
                var hash = hashEnd < 0 ? rest : rest[..hashEnd];
                return new RebaseTodoCommit(hash, SubjectOf(hashEnd < 0 ? string.Empty : rest[(hashEnd + 1)..]));
            case "merge" when rest.StartsWith(MergeFlag + " ", StringComparison.Ordinal):
                var afterFlag = rest[(MergeFlag.Length + 1)..].TrimStart();
                var end = afterFlag.IndexOf(' ', StringComparison.Ordinal);
                if (end > 0)
                {
                    var argument = afterFlag[(end + 1)..].Trim();
                    return new RebaseTodoCommit(
                        afterFlag[..end],
                        SubjectOf(argument),
                        RebaseTodoAction.Merge,
                        argument);
                }

                break;
        }

        throw new RebaseRangeException($"Git's todo has a \"{word}\" command Gitunia cannot carry: {line}");
    }

    private static string ResetName(string rest)
    {
        if (rest.StartsWith(NewRoot, StringComparison.Ordinal))
        {
            return NewRoot;
        }

        var end = rest.IndexOf(' ', StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    // "# subject" (recent git) or the bare subject (older git) for a pick; the part after "# " of a merge.
    private static string SubjectOf(string text)
    {
        if (text.StartsWith('#'))
        {
            return text.TrimStart('#').Trim();
        }

        var mark = text.IndexOf(OnelineMark, StringComparison.Ordinal);
        return mark < 0 ? text.Trim() : text[(mark + OnelineMark.Length)..].Trim();
    }
}
