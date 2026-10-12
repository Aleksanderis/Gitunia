namespace Gitunia.Core.Diff;

/// <summary>Pure transitions of a diff search: open, edit, accept, step, cancel. Matching is a case-insensitive substring test on the row text.</summary>
public static class DiffSearch
{
    public static DiffSearchUpdate Open() =>
        new(new DiffSearchState(DiffSearchPhase.Editing, string.Empty, [], string.Empty), null);

    public static DiffSearchUpdate Edit(DiffSearchState state, string text, DiffDocument document) =>
        new(state with { Phase = DiffSearchPhase.Editing, Text = text, Matches = FindMatches(document, text), Notice = string.Empty }, null);

    /// <summary>Ends typing: jumps to the first match at or after <paramref name="row"/>, or drops the search when nothing matches.</summary>
    public static DiffSearchUpdate Accept(DiffSearchState state, DiffDocument document, int row)
    {
        if (state.Text.Length == 0)
        {
            return new DiffSearchUpdate(DiffSearchState.Idle, null);
        }

        var matches = FindMatches(document, state.Text);
        if (matches.Count == 0)
        {
            return new DiffSearchUpdate(DiffSearchState.Idle with { Notice = "No match: " + state.Text }, null);
        }

        var target = matches.FirstOrDefault(match => match >= row, matches[0]);
        return new DiffSearchUpdate(state with { Phase = DiffSearchPhase.Active, Matches = matches, Notice = string.Empty }, target);
    }

    /// <summary>Moves to the <paramref name="count"/>th match after (or before) <paramref name="row"/>, wrapping around the ends.</summary>
    public static DiffSearchUpdate Step(DiffSearchState state, bool forward, int count, int row)
    {
        if (state.Phase != DiffSearchPhase.Active || state.Matches.Count == 0)
        {
            return new DiffSearchUpdate(state with { Notice = "No search" }, null);
        }

        var matches = state.Matches;
        var steps = Math.Max(1, count);
        var current = row;
        var wrapped = false;
        for (var step = 0; step < steps; step++)
        {
            var next = forward
                ? matches.Where(match => match > current).Cast<int?>().FirstOrDefault()
                : matches.Where(match => match < current).Cast<int?>().LastOrDefault();
            if (next is null)
            {
                wrapped = true;
                next = forward ? matches[0] : matches[^1];
            }

            current = next.Value;
        }

        return new DiffSearchUpdate(state with { Notice = wrapped ? "search wrapped" : string.Empty }, current);
    }

    public static DiffSearchUpdate Cancel() => new(DiffSearchState.Idle, null);

    /// <summary>Rows whose text contains <paramref name="text"/>, ignoring case; none for an empty query.</summary>
    public static IReadOnlyList<int> FindMatches(DiffDocument document, string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var matches = new List<int>();
        for (var row = 0; row < document.Rows.Count; row++)
        {
            if (document.Rows[row].Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(row);
            }
        }

        return matches;
    }
}
