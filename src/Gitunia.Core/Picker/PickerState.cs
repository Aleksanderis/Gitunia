using System.Collections.Immutable;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Picker;

/// <summary>The open repo picker: what was typed, the filtered rows and which one is highlighted. A closed picker is simply no state.</summary>
/// <param name="Query">The typed text: a fuzzy query, or a path when <see cref="IsPathMode"/>.</param>
/// <param name="Highlight">Index of the highlighted row, 0 when there are no rows.</param>
/// <param name="Rows">The rows matching the query, best first.</param>
/// <param name="Message">A notice such as "not a git repository", empty when none.</param>
/// <param name="LocatingTabId">Set when the picker was opened to find the new place of a missing repository: the chosen repository replaces that tab.</param>
/// <param name="IsListing">The directory candidates of a typed path are still being listed in the background; the rows of the previous query stay until it completes.</param>
/// <param name="IsScanningShown">The listing has taken long enough (about 150 ms) to show a "scanning..." notice.</param>
public sealed record PickerState(string Query, int Highlight, ImmutableArray<PickerRow> Rows, string Message, TabId? LocatingTabId, bool IsListing = false, bool IsScanningShown = false)
{
    /// <summary>The query is a path (starts with <c>/</c>, <c>~/</c>, <c>./</c>, <c>../</c> or a drive).</summary>
    public bool IsPathMode => PathQuery.IsPath(Query);

    /// <summary>The highlighted row, or null when the list is empty.</summary>
    public PickerRow? HighlightedRow => Highlight >= 0 && Highlight < Rows.Length ? Rows[Highlight] : null;
}
