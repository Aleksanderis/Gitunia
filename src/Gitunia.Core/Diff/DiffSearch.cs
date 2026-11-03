using Gitunia.Core.Editing;

namespace Gitunia.Core.Diff;

/// <summary>Pure transitions of a diff search: open, edit, accept, step, cancel. Matching is a case-insensitive substring test on the row text.</summary>
public static class DiffSearch
{
    public static DiffSearchUpdate Open() =>
        new(new DiffSearchState(DiffSearchPhase.Editing, string.Empty, [], string.Empty), null);

    public static DiffSearchUpdate Edit(DiffSearchState state, string text, DiffDocument document) =>
        Edit(state, text, PromptCaret.End, document);

    /// <summary>The search after an edit; a move of the caret alone keeps the matches and the notice.</summary>
    public static DiffSearchUpdate Edit(DiffSearchState state, string text, int caret, DiffDocument document)
    {
        if (text == state.Text && state.Phase == DiffSearchPhase.Editing)
        {
            return new DiffSearchUpdate(state with { Caret = caret }, null);
        }

        var edited = state with
        {
            Phase = DiffSearchPhase.Editing,
            Text = text,
            Caret = caret,
            Matches = FindMatches(document, text),
            Notice = string.Empty,
        };

        return new DiffSearchUpdate(edited, null);
    }

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
            return new DiffSearchUpdate(DiffSearchState.Idle with { Notice = $"No match: {state.Text}" }, null);
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
            var position = LowerBound(matches, forward ? current + 1 : current);
            var index = forward ? position : position - 1;
            if (index < 0 || index >= matches.Count)
            {
                wrapped = true;
                index = forward ? 0 : matches.Count - 1;
            }

            current = matches[index];
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

    /// <summary>Index of the first match that is at least <paramref name="row"/>, or the match count when there is none.</summary>
    private static int LowerBound(IReadOnlyList<int> matches, int row)
    {
        var low = 0;
        var high = matches.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (matches[middle] < row)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
