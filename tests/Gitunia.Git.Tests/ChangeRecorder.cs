namespace Gitunia.Git.Tests;

/// <summary>Collects what a <see cref="RepositoryWatcher"/> reports and lets a test wait for a condition instead of sleeping.</summary>
public sealed class ChangeRecorder
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly object gate = new();
    private RepositoryChangeKinds seen;
    private int callCount;

    public int CallCount
    {
        get
        {
            lock (gate)
            {
                return callCount;
            }
        }
    }

    public RepositoryChangeKinds Seen
    {
        get
        {
            lock (gate)
            {
                return seen;
            }
        }
    }

    public void Record(RepositoryChangeKinds kinds)
    {
        lock (gate)
        {
            seen |= kinds;
            callCount++;
        }
    }

    /// <summary>Waits until everything in <paramref name="expected"/> has been reported at least once.</summary>
    public async Task WaitForAsync(RepositoryChangeKinds expected, CancellationToken cancellationToken)
    {
        await WaitUntilAsync(() => (Seen & expected) == expected, "kinds " + expected, cancellationToken);
    }

    public async Task WaitUntilAsync(Func<bool> condition, string description, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        while (!condition())
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for {description}; saw {Seen}.");
            }
        }
    }
}
