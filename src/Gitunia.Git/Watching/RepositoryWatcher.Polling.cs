using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

public sealed partial class RepositoryWatcher
{
    private void FallBackToPolling()
    {
        lock (_gate)
        {
            FallBackToPollingLocked();
        }
    }

    private void FallBackToPollingLocked()
    {
        if (_isDisposed || _pollTimer is not null)
        {
            return;
        }

        StartPolling();
        // Anything may have been missed while the watchers were failing.
        EnqueueLocked(RepositoryChangeKinds.All);
    }

    private void StartPolling()
    {
        ReleaseWatchers();
        _lastFingerprint = RepositoryFingerprint.Compute(_location);
        _pollInterval = _options.PollInterval;

        // One-shot: the next poll is scheduled when this one ends, so callbacks never overlap and the wait can change.
        _pollTimer = new Timer(_ => Poll(), null, _pollInterval, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// A refresh this watcher caused took <paramref name="took"/> (or failed or timed out, <paramref name="failed"/>): while the
    /// watcher polls, the next poll waits at least five times as long, and doubles for each failure in a row (#1307).
    /// No effect while the watcher watches files.
    /// </summary>
    public void RecordRefresh(TimeSpan took, bool failed)
    {
        lock (_gate)
        {
            _pollFailures = failed ? _pollFailures + 1 : 0;
            _pollInterval = RefreshBackoff.Interval(
                _options.PollInterval,
                _options.PollIntervalLimit,
                _options.PollBackoffFactor,
                took,
                _pollFailures);
        }
    }

    private void ReleaseWatchers()
    {
        // Disposing can wait for in-flight handlers, which take the gate, so it happens off this thread.
        RetireWatchers([.. _watchers, .. _directoryWatchers.Values]);
        _watchers.Clear();
        _directoryWatchers.Clear();
        _watchedPaths.Clear();
        _budget.Release(_heldSlots);
        _heldSlots = 0;
    }

    private static void RetireWatchers(FileSystemWatcher[] retired) =>
        Task.Run(() =>
        {
            foreach (var watcher in retired)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        });

    private void Poll()
    {
        lock (_gate)
        {
            // A hidden tab does not poll: activating it polls at once (see SetActive).
            if (_isDisposed || !_isActive)
            {
                return;
            }
        }

        var fingerprint = RepositoryFingerprint.Compute(_location);
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            // Working tree edits cannot be seen cheaply, so a polling tab asks for a status refresh on every tick;
            // the refresher drops it when nothing differs.
            var kinds = RepositoryChangeKinds.WorkingTree;
            kinds |= fingerprint.ChangedKinds(_lastFingerprint);
            _lastFingerprint = fingerprint;

            EnqueueLocked(kinds);
            _pollTimer?.Change(_pollInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void Enqueue(RepositoryChangeKinds kinds)
    {
        lock (_gate)
        {
            EnqueueLocked(kinds);
        }
    }

    private void EnqueueLocked(RepositoryChangeKinds kinds)
    {
        if (_isDisposed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_pending == RepositoryChangeKinds.None)
        {
            _pendingSince = now;
        }

        var wasPending = _pending != RepositoryChangeKinds.None;
        _pending |= kinds;
        if (!_isActive)
        {
            if (!wasPending)
            {
                _queueNotification(RaiseBecameStale);
            }

            return;
        }

        // Wait for quiet, but never longer than the maximum delay since the first event.
        var remaining = _options.MaximumDelay - (now - _pendingSince);
        var delay = remaining < _options.Debounce ? remaining : _options.Debounce;
        _debounceTimer.Change(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
    }

    private void RaiseBecameStale() => BecameStale?.Invoke();

    private void OnDebounceElapsed()
    {
        RepositoryChangeKinds report;
        lock (_gate)
        {
            if (_isDisposed || !_isActive || _pending == RepositoryChangeKinds.None)
            {
                return;
            }

            report = _pending;
            _pending = RepositoryChangeKinds.None;
        }

        Changed?.Invoke(report);
    }
}
