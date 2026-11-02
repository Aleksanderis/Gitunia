using System.Collections.Immutable;
using Gitunia.Git;

namespace Gitunia.Core.Search.Models;

/// <summary>A change to a tab's <see cref="GraphSearchState"/>.</summary>
public abstract record GraphSearchCommand
{
    private protected GraphSearchCommand()
    {
    }
}

/// <summary>Open the search box with empty text.</summary>
public sealed record OpenSearchCommand : GraphSearchCommand;

/// <summary>The text in the search box changed.</summary>
/// <param name="Text">The whole text.</param>
public sealed record EditSearchTextCommand(string Text) : GraphSearchCommand;

/// <summary>Enter in the search box: keep the matches and jump to the first one at or after the selection.</summary>
/// <param name="SelectedIndex">The selected row.</param>
public sealed record AcceptSearchCommand(int SelectedIndex) : GraphSearchCommand;

/// <summary>Close the search box or clear the highlight.</summary>
public sealed record CancelSearchCommand : GraphSearchCommand;

/// <summary><c>n</c> or <c>N</c>: jump to the next or previous match, wrapping around.</summary>
/// <param name="Forward">True for <c>n</c>.</param>
/// <param name="Count">How many matches to step.</param>
/// <param name="SelectedIndex">The selected row.</param>
public sealed record StepMatchCommand(bool Forward, int Count, int SelectedIndex) : GraphSearchCommand;

/// <summary>A scan slice finished.</summary>
/// <param name="Generation">The generation the slice was scanned for.</param>
/// <param name="Matches">Matches of the slice, ascending.</param>
/// <param name="NextIndex">First row not scanned yet.</param>
/// <param name="LoadedCount">Rows loaded at that moment.</param>
/// <param name="LoadComplete">The log has finished loading.</param>
public sealed record ScanProgressCommand(int Generation, ImmutableArray<int> Matches, int NextIndex, int LoadedCount, bool LoadComplete) : GraphSearchCommand;

/// <summary>Rows were appended to the graph, or loading finished.</summary>
/// <param name="LoadedCount">Rows loaded now.</param>
/// <param name="LoadComplete">The log has finished loading.</param>
public sealed record GraphGrewCommand(int LoadedCount, bool LoadComplete) : GraphSearchCommand;

/// <summary>The graph was replaced by a refreshed log: matches and scan progress are stale and the scan starts over.</summary>
// ReSharper disable once NotAccessedPositionalProperty.Global : Kept for equality and the command log
public sealed record GraphReplacedCommand(int LoadedCount) : GraphSearchCommand;

/// <summary>Git answered a <c>body:</c> query, or failed to.</summary>
/// <param name="Generation">The generation the question was asked for.</param>
/// <param name="Hits">The commits whose message contains the text; null when the search failed.</param>
/// <param name="Error">Why it failed, empty on success.</param>
public sealed record BodyHitsCommand(int Generation, IReadOnlySet<ObjectId>? Hits, string Error) : GraphSearchCommand;
