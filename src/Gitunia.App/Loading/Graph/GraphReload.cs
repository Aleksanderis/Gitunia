using Gitunia.Core.Graph;

namespace Gitunia.App.Loading.Graph;

/// <summary>The graph rows after a refresh and how many of them were taken over from the previous graph instead of being read again.</summary>
/// <param name="Entries">All rows of the refreshed graph.</param>
/// <param name="ReusedCount">Rows reused from the previous graph (0 after a full read).</param>
/// <param name="TruncatedTotal">The commits in the whole log when the read stopped at the soft row limit; null when
/// it read everything.</param>
public sealed record GraphReload(List<GraphEntry> Entries, int ReusedCount, int? TruncatedTotal = null);
