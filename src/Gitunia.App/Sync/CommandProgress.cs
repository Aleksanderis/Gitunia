using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.App.Sync;

/// <summary>
/// Turns the reports of git into <see cref="SyncProgressed"/> commands for the tab, at most one per
/// <see cref="Interval"/>. A report that arrives sooner is kept (the latest wins) until <see cref="Flush"/>, so the
/// last value is never lost.
/// </summary>
/// <param name="dispatcher">Where the commands go (any thread may report).</param>
/// <param name="tabId">The tab the transfer runs for.</param>
/// <param name="time">The clock for the interval (null: the system clock).</param>
internal sealed class CommandProgress(IDispatcher dispatcher, TabId tabId, TimeProvider? time = null)
    : IProgress<RemoteProgress>
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private readonly Lock _gate = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private long _lastPosted;
    private bool _hasPosted;
    private RemoteProgress? _pending;

    public void Report(RemoteProgress value)
    {
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            if (_hasPosted && _time.GetElapsedTime(_lastPosted, now) < Interval)
            {
                _pending = value;
                return;
            }

            _lastPosted = now;
            _hasPosted = true;
            _pending = null;
            dispatcher.Post(new SyncProgressed(tabId, value));
        }
    }

    /// <summary>Posts the report that was held back, if any. Call when the transfer has ended.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_pending is not { } value)
            {
                return;
            }

            _pending = null;
            dispatcher.Post(new SyncProgressed(tabId, value));
        }
    }
}
