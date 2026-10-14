using System.Collections.Immutable;

namespace Gitunia.Core.Search.Models;

/// <summary>The outcome of scanning one slice of the graph.</summary>
/// <param name="Matches">Indexes of the matching rows in this slice, ascending.</param>
/// <param name="NextIndex">The first row the next slice starts at.</param>
public sealed record GraphScanResult(ImmutableArray<int> Matches, int NextIndex);
