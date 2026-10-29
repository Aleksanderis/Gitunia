namespace Gitunia.Core.Overlays;

/// <summary>
/// The candidates of a type-to-filter prompt and the highlighted one (ux.md, "Prompts and pickers"). Every picker
/// (remote, tag remote, upstream, branch checkout, remote branch to delete) keeps its candidates here and shows them
/// as a visible list under the text field with one hint line, so the choice is on screen and <c>Enter</c> runs the
/// highlighted one. Plain data and no terminal, so the behaviour is testable.
/// </summary>
/// <typeparam name="T">What a candidate is (a name, a remote, a branch).</typeparam>
public sealed class FilterPicker<T>
{
    private IReadOnlyList<T> _candidates = [];
    private int _index;

    /// <summary>The candidates, best match first.</summary>
    public IReadOnlyList<T> Candidates => _candidates;

    /// <summary>The highlighted candidate, or false when nothing matches.</summary>
    public bool TryGetSelected(out T selected)
    {
        if (_index < _candidates.Count)
        {
            selected = _candidates[_index];
            return true;
        }

        selected = default!;
        return false;
    }

    /// <summary>Replaces the candidates and highlights the first.</summary>
    public void Reset(IReadOnlyList<T> candidates)
    {
        _candidates = candidates;
        _index = 0;
    }

    /// <summary>Keeps the candidates matching <paramref name="query"/>, best first, and highlights the first.</summary>
    /// <param name="all">Every candidate.</param>
    /// <param name="query">What was typed; trimmed here.</param>
    /// <param name="text">The text of a candidate that the query is matched against.</param>
    /// <param name="score">The matcher: null when the candidate does not match, higher is better.</param>
    public void Filter(IEnumerable<T> all, string query, Func<T, string> text, Func<string, string, int?> score)
    {
        var pattern = query.Trim();
        Reset([
            .. all
                .Select(candidate => (Candidate: candidate, Score: score(pattern, text(candidate))))
                .Where(pair => pair.Score is not null)
                .OrderByDescending(pair => pair.Score)
                .Select(pair => pair.Candidate),
        ]);
    }

    /// <summary>Moves the highlight by <paramref name="step"/>, wrapping around.</summary>
    public void Move(int step)
    {
        var count = _candidates.Count;
        _index = count == 0 ? 0 : (((_index + step) % count) + count) % count;
    }

    /// <summary>The prompt: the field, the candidate list and the one hint line.</summary>
    /// <param name="title">The popup title.</param>
    /// <param name="context">What the answer is for.</param>
    /// <param name="input">The text typed so far.</param>
    /// <param name="acceptLabel">What <c>Enter</c> does, such as <c>choose</c>.</param>
    /// <param name="row">How a candidate is written in the list.</param>
    /// <param name="none">The row shown when nothing matches.</param>
    public PromptPopup ToPopup(
        string title,
        string context,
        string input,
        string acceptLabel,
        Func<T, string> row,
        string none) =>
        PromptPopup.ForFilter(title, context, input, Rows(row, none), acceptLabel, _candidates.Count > 1);

    /// <summary>
    /// The visible rows: a window of the candidates with <c>&gt; </c> on the highlighted one, then the position
    /// (<c>(2/5)</c>); <paramref name="none"/> alone when the list is empty.
    /// </summary>
    public IReadOnlyList<string> Rows(Func<T, string> row, string none)
    {
        if (_candidates.Count == 0)
        {
            return [none];
        }

        var rows = PickerRows.Window([.. _candidates.Select(row)], _index);
        rows.Add(PickerRows.Position(_index, _candidates.Count));
        return rows;
    }
}
