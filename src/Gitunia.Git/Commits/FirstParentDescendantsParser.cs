namespace Gitunia.Git.Commits;

/// <summary>
/// Reads <c>rev-list --parents --topo-order --reverse --ancestry-path</c> output (the descendants of one commit) into
/// the ones whose first-parent chain reaches that commit.
/// </summary>
public static class FirstParentDescendantsParser
{
    private const char LineSeparator = '\n';

    private const char FieldSeparator = ' ';

    /// <summary>
    /// Lines are <c>&lt;id&gt; &lt;parent&gt; &lt;parent&gt;...</c> with parents listed before their children and
    /// <paramref name="commit"/> itself left out. A commit joins the result when its first parent is
    /// <paramref name="commit"/> or already in the result; other lines (merges that only reach
    /// <paramref name="commit"/> through a second parent) and unreadable lines are skipped.
    /// </summary>
    public static HashSet<ObjectId> Parse(string output, ObjectId commit)
    {
        var descendants = new HashSet<ObjectId>();
        var remaining = output.AsSpan();
        while (!remaining.IsEmpty)
        {
            var end = remaining.IndexOf(LineSeparator);
            var line = end < 0 ? remaining : remaining[..end];
            remaining = end < 0 ? default : remaining[(end + 1)..];
            AddIfFirstParentDescendant(line, commit, descendants);
        }

        return descendants;
    }

    private static void AddIfFirstParentDescendant(
        ReadOnlySpan<char> line,
        ObjectId commit,
        HashSet<ObjectId> descendants)
    {
        var idEnd = line.IndexOf(FieldSeparator);
        if (idEnd < 0)
        {
            return;
        }

        var parents = line[(idEnd + 1)..];
        var parentEnd = parents.IndexOf(FieldSeparator);
        var firstParent = parentEnd < 0 ? parents : parents[..parentEnd];
        if (ObjectId.TryParse(line[..idEnd], out var id)
            && ObjectId.TryParse(firstParent, out var firstParentId)
            && (firstParentId == commit || descendants.Contains(firstParentId)))
        {
            descendants.Add(id);
        }
    }
}
