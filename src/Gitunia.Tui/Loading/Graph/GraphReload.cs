using Gitunia.Core.Graph;

namespace Gitunia.Tui.Loading.Graph;

/// <summary>The graph rows after a refresh and how many of them were taken over from the previous graph instead of being read again.</summary>
/// <param name="Entries">All rows of the refreshed graph.</param>
/// <param name="ReusedCount">Rows reused from the previous graph (0 after a full read).</param>
public sealed record GraphReload(List<GraphEntry> Entries, int ReusedCount);
