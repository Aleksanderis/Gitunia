using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;

namespace Gitunia.Core.Filtering;

/// <summary>
/// "Choose one from a filtered list" as immutable data, the one state every filter surface shares (the prompt picker, the
/// palette, the reflog ref picker, the ref jump): a <see cref="TextField"/> query, the matcher chosen for the list, the
/// candidates it keeps (best first for <see cref="MatcherKind.Fuzzy"/>, in the list's order for
/// <see cref="MatcherKind.Contains"/>), the highlighted row (a <see cref="ListViewport"/> over the candidates; moves stop
/// at the ends) and the text of the state without rows (<see cref="EmptyText"/>). Pure.
/// </summary>
/// <typeparam name="T">The item.</typeparam>
/// <param name="Items">Every item with the texts the query matches.</param>
/// <param name="Field">The typed query and its caret.</param>
/// <param name="Kind">The matcher.</param>
/// <param name="NoMatch">The text when nothing matches (from <see cref="UiTexts.NoMatch"/>).</param>
/// <param name="Candidates">The items matching the query.</param>
/// <param name="Selected">The highlighted candidate.</param>
/// <param name="Status">Ready, still loading or failed.</param>
/// <param name="Failure">What went wrong when <paramref name="Status"/> is failed.</param>
public sealed record FilterList<T>(
    ImmutableArray<FilterItem<T>> Items,
    TextField Field,
    MatcherKind Kind,
    string NoMatch,
    ImmutableArray<T> Candidates,
    int Selected = 0,
    FilterStatus Status = FilterStatus.Ready,
    string? Failure = null)
{
    /// <summary>The typed query.</summary>
    public string Text => Field.Text;

    /// <summary>Number of candidates.</summary>
    public int Count => Candidates.Length;

    /// <summary>The highlighted candidate, or false when there is none.</summary>
    public bool TryGetSelected(out T selected)
    {
        if (Selected >= 0 && Selected < Candidates.Length)
        {
            selected = Candidates[Selected];

            return true;
        }

        selected = default!;

        return false;
    }

    /// <summary>The highlighted candidate; <paramref name="fallback"/> when there is none.</summary>
    public T SelectedOr(T fallback) => TryGetSelected(out var selected) ? selected : fallback;

    /// <summary>The list after an edit of the query: a caret move keeps the highlight, other text starts at the best match.</summary>
    public FilterList<T> WithField(TextField field) =>
        field.Text == Field.Text ? this with { Field = field } : (this with { Field = field }).Refiltered([]);

    /// <summary>The same query over other items (the highlight goes back to the first row).</summary>
    public FilterList<T> WithItems(ImmutableArray<FilterItem<T>> items) =>
        (this with { Items = items, Status = FilterStatus.Ready, Failure = null }).Refiltered([]);

    /// <summary>
    /// The candidates with <paramref name="leading"/> in front of the matches (items found by other means, such as a commit
    /// hash), the highlight on the first.
    /// </summary>
    public FilterList<T> WithLeading(ImmutableArray<T> leading) => Refiltered(leading);

    /// <summary>The highlight moved by <paramref name="delta"/> rows, stopping at the ends.</summary>
    public FilterList<T> Move(int delta) =>
        this with { Selected = ListViewport.ClampIndex(Selected + delta, Candidates.Length) };

    /// <summary>Highlights the candidate at <paramref name="index"/>, clamped into the list.</summary>
    public FilterList<T> MoveTo(int index) =>
        this with { Selected = ListViewport.ClampIndex(index, Candidates.Length) };

    /// <summary>The list while its items are still coming.</summary>
    public FilterList<T> Loading() => this with { Status = FilterStatus.Loading, Failure = null };

    /// <summary>The list whose items could not be read.</summary>
    public FilterList<T> Failed(string failure) => this with { Status = FilterStatus.Failed, Failure = failure };

    /// <summary>The window of <paramref name="rows"/> rows around the highlight, kept centred like every overlay.</summary>
    public ListViewport Viewport(int rows) =>
        ListViewport.Of(Selected, Candidates.Length, rows).Follow(ScrollPolicy.Centre);

    /// <summary>The position of the highlight (<c>n/m</c>), or null when there are no candidates.</summary>
    public string? Position => Candidates.Length == 0 ? null : PositionText.Of(Selected, Candidates.Length);

    /// <summary>
    /// What the list says instead of rows: the loading text, the failure, or the no-match text; null when it has
    /// candidates. Always drawn in the empty-text role.
    /// </summary>
    public string? EmptyText => Status switch
    {
        FilterStatus.Loading => UiTexts.Loading,
        FilterStatus.Failed => Failure ?? NoMatch,
        _ => Candidates.Length == 0 ? NoMatch : null,
    };

    private FilterList<T> Refiltered(ImmutableArray<T> leading)
    {
        var query = Field.Text.Trim();
        var matches = Match(query);

        return this with { Candidates = [.. leading, .. matches], Selected = 0 };
    }

    private ImmutableArray<T> Match(string query)
    {
        if (query.Length == 0)
        {
            return [.. Items.OrderByDescending(item => item.Bonus).Select(item => item.Value)];
        }

        var scored = new List<Scored>();
        foreach (var item in Items)
        {
            if (BestScore(query, item) is { } score)
            {
                scored.Add(new Scored(item, score));
            }
        }

        if (Items.Any(item => item.Group is not null))
        {
            return Grouped(scored);
        }

        return [.. scored.OrderByDescending(entry => entry.Score).Select(entry => entry.Item.Value)];
    }

    // Groups stay together: the group holding the best match first, groups without a match gone.
    private static ImmutableArray<T> Grouped(List<Scored> scored)
    {
        var best = scored
            .GroupBy(entry => entry.Item.Group ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Max(entry => entry.Score));

        return
        [
            .. scored
                .OrderByDescending(entry => best[entry.Item.Group ?? string.Empty])
                .ThenBy(entry => entry.Item.Group, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.Score)
                .Select(entry => entry.Item.Value),
        ];
    }

    private int? BestScore(string query, FilterItem<T> item)
    {
        int? best = null;
        foreach (var text in item.Texts)
        {
            if (Matcher.Score(Kind, query, text) is { } score && (best is null || score > best))
            {
                best = score;
            }
        }

        return best + item.Bonus;
    }

    private readonly record struct Scored(FilterItem<T> Item, int Score);
}
