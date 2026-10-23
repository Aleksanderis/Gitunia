using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Tests.Watching;

/// <summary>Collects what a <see cref="RepositoryWatcher"/> reports and lets a test wait for a condition instead of sleeping.</summary>
public sealed class ChangeRecorder
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private RepositoryChangeKinds _seen;
    private int _callCount;
    private TaskCompletionSource _changed = NewSignal();

    public int CallCount
    {
        get
        {
            lock (_gate)
            {
                return _callCount;
            }
        }
    }

    public RepositoryChangeKinds Seen
    {
        get
        {
            lock (_gate)
            {
                return _seen;
            }
        }
    }

    public void Record(RepositoryChangeKinds kinds)
    {
        lock (_gate)
        {
            _seen |= kinds;
            _callCount++;
            _changed.TrySetResult();
            _changed = NewSignal();
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
            lock (_gate)
            {
                if ((_seen & expected) == expected)
                {
                    return;
                }

                signal = _changed.Task;
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

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
