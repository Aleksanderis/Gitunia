using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Picker;

/// <summary>The open repository picker: what was typed, the filtered rows and which one is highlighted. A closed picker is simply no state.</summary>
/// <param name="Query">The typed text: a fuzzy query, or a path when <see cref="IsPathMode"/>.</param>
/// <param name="Highlight">Index of the highlighted row, 0 when there are no rows (the anchor row while <paramref name="IsOnToggle"/>).</param>
/// <param name="Rows">The rows matching the query, best first.</param>
/// <param name="Message">A notice such as "not a git repository", empty when none.</param>
/// <param name="LocatingTabId">Set when the picker was opened to find the new place of a missing repository: the chosen repository replaces that tab.</param>
/// <param name="IsListing">The directory candidates of a typed path are still being listed in the background; the rows of the previous query stay until it completes.</param>
/// <param name="IsScanningShown">The listing has taken long enough (about 150 ms) to show a "scanning..." notice.</param>
/// <param name="Caret">The caret column in <see cref="Query"/> (see <see cref="PromptCaret"/>); the end when the query is replaced as a value.</param>
/// <param name="Expanded">The sections the user expanded past their caps (see <see cref="PickerListPlan"/>); gone when the picker closes.</param>
/// <param name="IsOnToggle">The highlight sits on the "more" or "less" line that follows row <see cref="Highlight"/> (the last listed row of its section), not on a row.</param>
public sealed record PickerState(string Query, int Highlight, ImmutableArray<PickerRow> Rows, string Message, TabId? LocatingTabId, bool IsListing = false, bool IsScanningShown = false, int Caret = PromptCaret.End, PickerSection Expanded = PickerSection.None, bool IsOnToggle = false)
{
    /// <summary>The query is a path (starts with <c>/</c>, <c>~/</c>, <c>./</c>, <c>../</c> or a drive).</summary>
    public bool IsPathMode => PathQuery.IsPath(Query);

    /// <summary>The highlighted row, or null when the list is empty or the highlight is on a "more" or "less" line.</summary>
    public PickerRow? HighlightedRow => !IsOnToggle && Highlight >= 0 && Highlight < Rows.Length ? Rows[Highlight] : null;
}
