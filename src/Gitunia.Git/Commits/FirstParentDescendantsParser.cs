namespace Gitunia.Git.Commits;

/// <summary>Reads <c>rev-list --parents --topo-order --reverse --ancestry-path</c> output into the first-parent descendants of one commit.</summary>
public static class FirstParentDescendantsParser
{
    private const char LineSeparator = '\n';

    private const char FieldSeparator = ' ';

    /// <summary>
    /// Lines are <c>&lt;id&gt; &lt;parent&gt; &lt;parent&gt;...</c> with parents listed before their children and <paramref name="commit"/>
    /// itself left out. A commit joins the result when its first parent is <paramref name="commit"/> or already in the result; other
    /// lines (merges that only reach <paramref name="commit"/> through a second parent) and unreadable lines are skipped.
    /// </summary>
    public static HashSet<ObjectId> Parse(string output, ObjectId commit)
    {
        var descendants = new HashSet<ObjectId>();
        foreach (var line in output.Split(LineSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 2
                && ObjectId.TryParse(fields[0], out var id)
                && ObjectId.TryParse(fields[1], out var firstParent)
                && (firstParent == commit || descendants.Contains(firstParent)))
            {
                descendants.Add(id);
            }
        }

        return descendants;
    }
}
