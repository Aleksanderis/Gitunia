namespace Gitunia.App.Background;

/// <summary>
/// Runs at most <see cref="DefaultLimit"/> background jobs at once; the others wait in arrival order. A git read is a
/// process, so the limit also bounds the processes background work can start (docs/internal/performance.md,
/// "Background work"). Thread safe.
/// </summary>
public sealed class BackgroundWork : IBackgroundWork, IDisposable
{
    /// <summary>Jobs that run at the same time (the user's own effects are outside this count).</summary>
    public const int DefaultLimit = 8;

    private readonly SemaphoreSlim _slots;
    private int _running;
    private int _waiting;

    public BackgroundWork(int limit = DefaultLimit)
    {
        _slots = new SemaphoreSlim(Math.Max(1, limit));
    }

    /// <summary>Jobs running now.</summary>
    public int Running => Volatile.Read(ref _running);

    /// <summary>Jobs waiting for a slot.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    public async Task RunAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _waiting);
        try
        {
            await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }

        Interlocked.Increment(ref _running);
        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _running);
            ReleaseSlot();
        }
    }

    /// <summary>Frees the semaphore. A job still running at shutdown frees its slot into nothing.</summary>
    public void Dispose() => _slots.Dispose();

    private void ReleaseSlot()
    {
        try
        {
            _slots.Release();
        }
        catch (ObjectDisposedException)
        {
            // Disposed with the runner while this job was running.
        }
    }
}
