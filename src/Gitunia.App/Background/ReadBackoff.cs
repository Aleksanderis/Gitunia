using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Watching;

namespace Gitunia.App.Background;

/// <summary>
/// Per-repository back-off of the background status pollers (#1307): a read that took longer than the pass interval, and
/// every failed or timed-out read, postpones that repository's next read, so a few unreachable repositories do not keep the
/// poller busy. A failure doubles the wait each time in a row; a success resets it. Safe to call from any thread.
/// </summary>
public sealed class ReadBackoff
{
    private const int DefaultFactor = 5;

    private static readonly TimeSpan DefaultMaximum = TimeSpan.FromMinutes(5);

    private readonly TimeSpan _interval;
    private readonly TimeSpan _maximum;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Entry> _entries = new(RepositoryPathComparer.Instance);
    private readonly object _gate = new();

    public ReadBackoff(TimeSpan interval, TimeProvider? time = null, TimeSpan? maximum = null)
    {
        _interval = interval;
        _maximum = maximum ?? DefaultMaximum;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>False while <paramref name="key"/> (a repository path) is postponed by its last read.</summary>
    public bool IsDue(string key)
    {
        lock (_gate)
        {
            return !_entries.TryGetValue(key, out var entry) || _time.GetUtcNow() >= entry.DueAt;
        }
    }

    /// <summary>The number of failed or timed-out reads in a row for <paramref name="key"/>.</summary>
    public int Failures(string key)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(key, out var entry) ? entry.Failures : 0;
        }
    }

    /// <summary>A read that took <paramref name="took"/> succeeded: it only postpones the next one when slower than a pass.</summary>
    public void RecordSuccess(string key, TimeSpan took)
    {
        var wait = RefreshBackoff.Interval(_interval, _maximum, DefaultFactor, took, 0) - _interval;
        Store(key, 0, wait);
    }

    /// <summary>A read failed or timed out after <paramref name="took"/>: skip this repository for a growing period.</summary>
    public void RecordFailure(string key, TimeSpan took)
    {
        var failures = Failures(key) + 1;

        // Half a pass less than the interval, so the skip does not end just before a pass that should still skip it.
        var wait = RefreshBackoff.Interval(_interval, _maximum, DefaultFactor, took, failures) - (_interval / 2);
        Store(key, failures, wait);
    }

    private void Store(string key, int failures, TimeSpan wait)
    {
        lock (_gate)
        {
            _entries[key] = new Entry(failures, _time.GetUtcNow() + wait);
        }
    }

    private readonly record struct Entry(int Failures, DateTimeOffset DueAt);
}
