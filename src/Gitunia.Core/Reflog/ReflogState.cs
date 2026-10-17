using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reflog.Models;

namespace Gitunia.Core.Reflog;

/// <summary>The open reflog list: whose reflog, its entries (newest first) and the highlighted row. Pure.</summary>
/// <param name="TabId">The tab the list was opened in; the actions on an entry run in that tab.</param>
/// <param name="ReferenceName">The ref as git is asked for it (<c>HEAD</c> or <c>refs/heads/main</c>).</param>
/// <param name="Title">The ref as the user sees it (<c>HEAD</c> or <c>main</c>), the prefix of <c>main@{n}</c>.</param>
/// <param name="Entries">Every entry read.</param>
/// <param name="SelectedIndex">Index into <paramref name="Entries"/>.</param>
/// <param name="SearchText">The search text, empty when no search is active.</param>
/// <param name="IsEditingSearch">True while the search box takes typed text.</param>
/// <param name="Picker">The open ref picker, null when the list itself is shown.</param>
public sealed record ReflogState(
    TabId TabId,
    string ReferenceName,
    string Title,
    IReadOnlyList<ReflogEntry> Entries,
    int SelectedIndex = 0,
    string SearchText = "",
    bool IsEditingSearch = false,
    ReflogRefPicker? Picker = null)
{
    /// <summary>True when a search is active (being typed or accepted).</summary>
    public bool HasSearch => SearchText.Length > 0 || IsEditingSearch;

    /// <summary>Indexes of the entries matching the search; empty without one.</summary>
    public IReadOnlyList<int> MatchIndexes
    {
        get
        {
            if (SearchText.Length == 0)
            {
                return [];
            }

            var matches = new List<int>();
            for (var index = 0; index < Entries.Count; index++)
            {
                if (IsMatch(Entries[index]))
                {
                    matches.Add(index);
                }
            }

            return matches;
        }
    }

    /// <summary>True when the entry matches the search text, case-insensitive.</summary>
    public bool IsMatch(ReflogEntry entry) =>
        SearchText.Length > 0
        && (entry.Hash.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase)
            || $"{Title}@{{{entry.Index}}}".Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || entry.Action.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    /// <summary>Starts a fresh search in the box.</summary>
    public ReflogState OpenSearch() => this with { SearchText = string.Empty, IsEditingSearch = true };

    /// <summary>The search with new text; the highlight jumps to the first match at or after the highlight.</summary>
    public ReflogState EditSearch(string text) => (this with { SearchText = text }).JumpToMatch(true, 0, SelectedIndex);

    /// <summary>Leaves the box and keeps the search active (so <c>n</c> and <c>N</c> work).</summary>
    public ReflogState AcceptSearch() => this with { IsEditingSearch = false };

    /// <summary>Drops the search.</summary>
    public ReflogState ClearSearch() => this with { SearchText = string.Empty, IsEditingSearch = false };

    /// <summary>Steps <paramref name="count"/> matches forward or back from the highlight, wrapping around.</summary>
    public ReflogState StepMatch(bool forward, int count) => JumpToMatch(forward, Math.Max(1, count), SelectedIndex);

    // Count 0 means "the match at or after the start"; otherwise the count-th match strictly beyond it, wrapping.
    private ReflogState JumpToMatch(bool forward, int count, int start)
    {
        var matches = MatchIndexes;
        if (matches.Count == 0)
        {
            return this;
        }

        int position;
        if (count == 0)
        {
            position = Math.Max(0, matches.ToList().FindIndex(index => index >= start));
        }
        else if (forward)
        {
            var next = matches.ToList().FindIndex(index => index > start);
            position = (Math.Max(0, next) + count - 1) % matches.Count;
        }
        else
        {
            var previous = matches.ToList().FindLastIndex(index => index < start);
            position = ((((previous < 0 ? matches.Count - 1 : previous) - count + 1) % matches.Count) + matches.Count)
                % matches.Count;
        }

        return MoveTo(matches[position]);
    }

    /// <summary>The highlighted entry, null when the reflog is empty.</summary>
    public ReflogEntry? Selected =>
        Entries.Count == 0 ? null : Entries[Math.Clamp(SelectedIndex, 0, Entries.Count - 1)];

    /// <summary>Moves the highlight to <paramref name="index"/>, clamped to the entries.</summary>
    public ReflogState MoveTo(int index) =>
        this with { SelectedIndex = Entries.Count == 0 ? 0 : Math.Clamp(index, 0, Entries.Count - 1) };

    /// <summary>The state for a fresh read of the same reflog: the highlight stays on the same row index.</summary>
    public ReflogState WithEntries(IReadOnlyList<ReflogEntry> entries) =>
        this with
        {
            Entries = entries,
            SelectedIndex = entries.Count == 0 ? 0 : Math.Min(SelectedIndex, entries.Count - 1),
        };
}
