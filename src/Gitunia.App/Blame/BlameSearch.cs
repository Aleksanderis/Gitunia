namespace Gitunia.App.Blame;

/// <summary>Finds lines of the blame view that contain a text. Pure.</summary>
public static class BlameSearch
{
    /// <summary>
    /// Steps over <paramref name="count"/> matching lines from <paramref name="start"/> (not included), wrapping
    /// around. A line matches when its text, the author or the subject of its commit contains the query, ignoring case.
    /// </summary>
    /// <param name="data">The lines.</param>
    /// <param name="query">The text.</param>
    /// <param name="start">The line to start after (or before, going back); -1 starts at the first line.</param>
    /// <param name="forward">The direction.</param>
    /// <param name="count">How many matches to step over, at least 1.</param>
    /// <returns>The line, -1 when no line matches.</returns>
    public static int Find(BlameData data, string query, int start, bool forward, int count)
    {
        var total = data.Lines.Count;
        if (total == 0)
        {
            return -1;
        }

        var step = forward ? 1 : -1;
        var line = start;
        var matches = new List<int>();
        for (var visited = 0; visited < total; visited++)
        {
            line = (((line + step) % total) + total) % total;
            if (IsMatch(data, line, query))
            {
                matches.Add(line);
            }
        }

        // Matches are in visiting order; a count beyond the last wraps around like vim's.
        return matches.Count == 0 ? -1 : matches[(Math.Max(1, count) - 1) % matches.Count];
    }

    private static bool IsMatch(BlameData data, int line, string query)
    {
        if (data.Lines[line].Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var owner = data.OwnerOf(line);

        return owner is not null
            && (owner.Author.Contains(query, StringComparison.OrdinalIgnoreCase)
                || owner.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
