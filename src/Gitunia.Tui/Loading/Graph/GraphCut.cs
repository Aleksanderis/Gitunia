namespace Gitunia.Tui.Loading.Graph;

/// <summary>
/// Filled in by <see cref="GraphLoader.StreamCappedAsync"/> when the log is longer than the soft row limit: how many
/// commits the whole log has. Read it after the stream has ended.
/// </summary>
public sealed class GraphCut
{
    /// <summary>The commits in the whole log when the stream stopped at the limit; null when all was read.</summary>
    public int? TruncatedTotal { get; set; }
}
