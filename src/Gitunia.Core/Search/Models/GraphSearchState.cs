using System.Collections.Immutable;
using Gitunia.Core.Editing;

namespace Gitunia.Core.Search.Models;

/// <summary>The graph search of one tab. Immutable; <see cref="GraphSearchReducer"/> produces the next one.</summary>
/// <param name="Phase">Where the flow is.</param>
/// <param name="Text">The text in the search box.</param>
/// <param name="Query">The parsed text, null while there is nothing to look for.</param>
/// <param name="Matches">Matching row indexes found so far, ascending.</param>
/// <param name="Scanned">Rows looked at so far.</param>
/// <param name="IsScanning">A scan slice is queued or running.</param>
/// <param name="IsComplete">Every row of the (fully loaded) graph was looked at.</param>
/// <param name="Generation">Counts query changes; late scan results of an older generation are dropped.</param>
/// <param name="Anchor">A pending jump goes to the first match after this row.</param>
/// <param name="JumpPending">A jump waits for the scan to find a match.</param>
/// <param name="Notice">A short message for the status bar, empty when none.</param>
/// <param name="Caret">The caret column in <see cref="Text"/> while typing (see <see cref="PromptCaret"/>).</param>
public sealed record GraphSearchState(
    SearchPhase Phase,
    string Text,
    SearchQuery? Query,
    ImmutableArray<int> Matches,
    int Scanned,
    bool IsScanning,
    bool IsComplete,
    int Generation,
    int Anchor,
    bool JumpPending,
    string Notice,
    int Caret = PromptCaret.End)
{
    public static GraphSearchState Initial { get; } = new(SearchPhase.Idle, string.Empty, null, [], 0, false, true, 0, -1, false, string.Empty);

    /// <summary>The search box is open or matches are shown.</summary>
    public bool IsOpen => Phase != SearchPhase.Idle;
}
