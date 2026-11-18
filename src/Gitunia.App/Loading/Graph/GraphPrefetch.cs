using Gitunia.Git.Repositories.Models;

namespace Gitunia.App.Loading.Graph;

/// <summary>
/// Starts the graph read of the repository given on the command line while discovery still resolves it (#1392): on
/// Windows a git process costs ~22 ms to start, so waiting for discovery put one serial spawn in front of the log. The
/// read runs from the given path (git finds the repository itself), which gives the same log as from the discovered
/// working tree or git directory. Only the location the owner confirms after a successful discovery can take it, once;
/// a failed discovery cancels it, and an unclaimed one expires.
/// </summary>
public sealed class GraphPrefetch : IDisposable
{
    /// <summary>How long an unclaimed read lives before it is cancelled.</summary>
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(10);

    private readonly GraphLoader _loader;
    private readonly TimeSpan _expiry;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private PrefetchedGraph? _pending;
    private RepositoryLocation? _confirmed;

    /// <param name="loader">Streams the rows.</param>
    /// <param name="expiry">How long an unclaimed read lives; <see cref="DefaultExpiry"/> when null.</param>
    /// <param name="clock">Runs the expiry timer; tests pass a clock they move by hand.</param>
    public GraphPrefetch(GraphLoader loader, TimeSpan? expiry = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _loader = loader;
        _expiry = expiry ?? DefaultExpiry;
    }

    /// <summary>Starts the read from <paramref name="path"/>; does nothing when the path is not an existing directory.</summary>
    public void Start(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            return;
        }

        var source = new CancellationTokenSource(_expiry, _clock);
        var cut = new GraphCut();
        var provisional = new RepositoryLocation(fullPath, fullPath, fullPath, false, false);
        var batches = _loader.StreamCappedAsync(provisional, cut, source.Token).GetAsyncEnumerator(source.Token);

        // On a pool thread: the start of the async iterator runs up to its first await on the calling thread.
        var first = Task.Run(() => batches.MoveNextAsync().AsTask(), CancellationToken.None);
        lock (_gate)
        {
            _pending = new PrefetchedGraph(batches, first, cut, source);
        }
    }

    /// <summary>Discovery succeeded: the read belongs to this repository.</summary>
    public void Confirm(RepositoryLocation location)
    {
        lock (_gate)
        {
            _confirmed = location;
        }
    }

    /// <summary>Discovery failed or the start is abandoned: cancels the read.</summary>
    public void Discard()
    {
        PrefetchedGraph? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }

        // The suspended enumerator is not disposed: cancelling kills git, the rest goes at process exit.
        pending?.Cancel();
        pending?.Dispose();
    }

    public void Dispose() => Discard();

    /// <summary>The read for <paramref name="location"/>, once, when it is the confirmed one and still alive.</summary>
    public PrefetchedGraph? Claim(RepositoryLocation location)
    {
        lock (_gate)
        {
            if (_pending is null || _confirmed is null || _confirmed != location || _pending.IsCancelled)
            {
                return null;
            }

            var claimed = _pending;
            _pending = null;
            _confirmed = null;
            claimed.KeepAlive();

            // The expiry may have fired between the check above and ending its timer.
            if (claimed.IsCancelled)
            {
                claimed.Dispose();

                return null;
            }

            return claimed;
        }
    }
}
