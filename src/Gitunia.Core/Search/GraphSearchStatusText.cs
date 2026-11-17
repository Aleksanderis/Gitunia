using System.Globalization;
using Gitunia.Core.Editing;
using Gitunia.Core.Search.Models;

namespace Gitunia.Core.Search;

/// <summary>The status bar text of a search: the typed query, the match position and the scan progress.</summary>
public static class GraphSearchStatusText
{
    /// <param name="state">The tab's search.</param>
    /// <param name="selectedIndex">The selected row, -1 when none.</param>
    /// <param name="loadedCount">Rows loaded so far.</param>
    /// <returns>The line (the caret only while typing), empty when no search is open.</returns>
    public static PromptLine Describe(GraphSearchState state, int selectedIndex, int loadedCount)
    {
        if (!state.IsOpen)
        {
            return PromptLine.Empty;
        }

        var field = new PromptLine("/", state.Text, state.Phase == SearchPhase.Editing ? state.Field.Caret : null);
        if (state.Query is null)
        {
            return field;
        }

        var matchCount = $"{state.Matches.Length.ToString(CultureInfo.InvariantCulture)}{(state.IsComplete ? string.Empty : "+")}";
        var position = state.Matches.IndexOf(selectedIndex);
        var summary = position >= 0 ? $"{(position + 1).ToString(CultureInfo.InvariantCulture)}/{matchCount}" : $"{matchCount} found";
        if (state.Query.IsAwaitingBodyHits)
        {
            summary = $"{summary} searching messages";
        }
        else if (!state.IsComplete && loadedCount > 0)
        {
            var percent = (int)Math.Min(100L, (long)state.Scanned * 100 / loadedCount);
            summary = $"{summary} searching {percent.ToString(CultureInfo.InvariantCulture)}%";
        }

        return field.WithSuffix($"  {summary}");
    }
}
