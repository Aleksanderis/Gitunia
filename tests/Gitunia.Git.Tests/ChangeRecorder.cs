namespace Gitunia.Git.Tests;

/// <summary>Collects what a <see cref="RepositoryWatcher"/> reports and lets a test wait for a condition instead of sleeping.</summary>
public sealed class ChangeRecorder
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly object gate = new();
    private RepositoryChangeKinds seen;
    private int callCount;
    private TaskCompletionSource changed = NewSignal();

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
            changed.TrySetResult();
            changed = NewSignal();
        }
    }

    /// <summary>Waits until everything in <paramref name="expected"/> has been reported at least once.</summary>
    public async Task WaitForAsync(RepositoryChangeKinds expected, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        while (true)
        {
            Task signal;
            lock (gate)
            {
                if ((seen & expected) == expected)
                {
                    return;
                }

                signal = changed.Task;
            }

            try
            {
                await signal.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for kinds {expected}; saw {Seen}.");
            }
        }
    }

    /// <summary>Polls a condition that has no signal of its own (for example a hidden watcher turning stale, which reports nothing).</summary>
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

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
