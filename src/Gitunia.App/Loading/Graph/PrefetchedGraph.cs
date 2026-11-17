using Gitunia.Core.Graph;

namespace Gitunia.App.Loading.Graph;

/// <summary>A graph read that was started before its repository was known: the stream, its first step and its cut.</summary>
public sealed class PrefetchedGraph : IDisposable
{
    private readonly CancellationTokenSource _source;

    public PrefetchedGraph(
        IAsyncEnumerator<IReadOnlyList<GraphEntry>> batches,
        Task<bool> first,
        GraphCut cut,
        CancellationTokenSource source)
    {
        Batches = batches;
        First = first;
        Cut = cut;
        _source = source;
    }

    /// <summary>The running stream; the owner disposes it.</summary>
    public IAsyncEnumerator<IReadOnlyList<GraphEntry>> Batches { get; }

    /// <summary>The first <c>MoveNextAsync</c>, already running.</summary>
    public Task<bool> First { get; }

    /// <summary>Receives the total when the log is cut.</summary>
    public GraphCut Cut { get; }

    /// <summary>Whether the read was cancelled (expired, or dropped because discovery failed).</summary>
    public bool IsCancelled => _source.IsCancellationRequested;

    /// <summary>Ends the expiry timer: the read now belongs to a tab, whose own cancellation stops it.</summary>
    public void KeepAlive() => _source.CancelAfter(Timeout.InfiniteTimeSpan);

    /// <summary>Cancels the read and kills git.</summary>
    public void Cancel() => _source.Cancel();

    public void Dispose() => _source.Dispose();
}
