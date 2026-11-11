using Gitunia.App.Tabs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App.Tabs;

/// <summary>
/// Notices changes made outside Gitunia without file watchers: while its tab is active it reads the repository
/// fingerprint every <see cref="PollOptions.RefsInterval"/> (off the UI thread) and asks for a refresh of what changed
/// (see <see cref="RepositoryFingerprintGroups"/>), else for a status refresh once the status interval has passed. Never overlaps itself, skips a tick while
/// the tab refreshes or runs an action, stops when the tab is hidden or disposed (a late result is dropped), and
/// lengthens the status interval after a slow refresh. All members except the timer callback run on the UI thread.
/// </summary>
public sealed class TabPoller : IDisposable
{
    private readonly RepositoryLocation _location;
    private readonly PollOptions _options;
    private readonly IUiInvoker _ui;
    private readonly IPollTarget _target;
    private readonly TimeProvider _time;
    private readonly ShutdownTasks _tasks;
    private readonly Func<RepositoryLocation, RepositoryFingerprintGroups> _fingerprint;
    private readonly Timer _timer;
    private RepositoryFingerprintGroups? _baseline;
    private long _statusDueAt;
    private long? _lastCheckAt;
    private int _failures;
    private TimeSpan _statusInterval;
    private int _epoch;
    private bool _isActive;
    private bool _isReading;
    private bool _isDisposed;

    public TabPoller(
        RepositoryLocation location,
        PollOptions options,
        IUiInvoker ui,
        IPollTarget target,
        TimeProvider? time = null,
        ShutdownTasks? tasks = null,
        Func<RepositoryLocation, RepositoryFingerprintGroups>? fingerprint = null)
    {
        _fingerprint = fingerprint ?? RepositoryFingerprint.Compute;
        _tasks = tasks ?? new ShutdownTasks();
        _location = location;
        _options = options;
        _ui = ui;
        _target = target;
        _time = time ?? TimeProvider.System;
        _statusInterval = options.StatusInterval;
        _timer = new Timer(_ => _ui.Post(Tick));
    }

    /// <summary>The wait between status refreshes right now (above the configured one after a slow refresh).</summary>
    public TimeSpan StatusInterval => _statusInterval;

    /// <summary>Takes the first fingerprint, so a change made while the tab loads is seen by the first tick.</summary>
    // The fingerprint is plain file reading that cannot be cancelled; the shutdown only waits for it (short) within its bound.
    public void Start() => _tasks.Run(() =>
    {
        ReadBaseline();

        return Task.CompletedTask;
    });

    /// <summary>
    /// A visible tab polls (and checks at once, so switching to it shows outside changes); a hidden one does not.
    /// </summary>
    public void SetActive(bool isActive)
    {
        if (_isDisposed || _isActive == isActive)
        {
            return;
        }

        _isActive = isActive;
        _epoch++;
        _isReading = false;
        if (!isActive)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            return;
        }

        _statusDueAt = _time.GetTimestamp();
        _timer.Change(TimeSpan.Zero, _options.RefsInterval);
    }

    /// <summary>
    /// Checks at once instead of at the next tick (the terminal gained focus): reads the fingerprint now and makes the
    /// status refresh due. Does nothing for a hidden tab, while a read runs or while the tab is busy.
    /// </summary>
    public void CheckNow()
    {
        if (_isDisposed || !_isActive)
        {
            return;
        }

        // Rapid focus changes (alt-tab, tmux panes) would start one status read each.
        var now = _time.GetTimestamp();
        if (_lastCheckAt is { } last && now - last < ToTimestampTicks(_options.FocusCheckGap))
        {
            return;
        }

        _lastCheckAt = now;
        _statusDueAt = now;
        Tick();
    }

    /// <summary>
    /// A refresh without a graph reload took <paramref name="took"/>: the next status refresh waits accordingly.
    /// </summary>
    public void RecordRefresh(TimeSpan took)
    {
        _failures = 0;
        _statusInterval = _options.StatusIntervalAfter(took);
        _statusDueAt = _time.GetTimestamp() + ToTimestampTicks(_statusInterval);
    }

    /// <summary>
    /// A refresh failed or timed out after <paramref name="took"/>: it counts like a slow one, and the wait doubles for each
    /// failure in a row until a refresh succeeds (<see cref="RecordRefresh"/>).
    /// </summary>
    public void RecordFailure(TimeSpan took)
    {
        _failures++;
        _statusInterval = _options.StatusIntervalAfter(took, _failures);
        _statusDueAt = _time.GetTimestamp() + ToTimestampTicks(_statusInterval);
    }

    /// <summary>
    /// Reads the fingerprint now, on the calling (background) thread. A refresh Gitunia runs itself takes it before its own
    /// reads, so its own writes are in it and an outside change made during the refresh still differs at the next tick.
    /// </summary>
    public RepositoryFingerprintGroups ReadFingerprint() => _fingerprint(_location);

    /// <summary>
    /// A refresh of <paramref name="covered"/> kinds that Gitunia ran itself has been applied: the groups it covered take
    /// their value from <paramref name="before"/> (read by <see cref="ReadFingerprint()"/> before that refresh), so its own
    /// writes (stage, commit) are not mistaken for outside changes. A read in flight is dropped: it may predate the accept.
    /// </summary>
    public void AcceptRefresh(RepositoryFingerprintGroups before, RepositoryChangeKinds covered)
    {
        if (_isDisposed || _baseline is not { } baseline)
        {
            return;
        }

        _baseline = baseline.Accept(before, covered);
        _epoch++;
        _isReading = false;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _epoch++;
        _timer.Dispose();
    }

    private void ReadBaseline()
    {
        var value = _fingerprint(_location);
        _ui.Post(() => _baseline ??= value);
    }

    private void Tick()
    {
        if (_isDisposed || !_isActive || _isReading || !_target.CanPoll)
        {
            return;
        }

        _isReading = true;
        var epoch = _epoch;
        _tasks.Run(() =>
        {
            ReadFingerprint(epoch);

            return Task.CompletedTask;
        });
    }

    private void ReadFingerprint(int epoch)
    {
        var value = _fingerprint(_location);
        _ui.Post(() => Compare(value, epoch));
    }

    private void Compare(RepositoryFingerprintGroups value, int epoch)
    {
        if (epoch != _epoch)
        {
            // Hidden, shown again or disposed meanwhile: the new epoch has its own read.
            return;
        }

        _isReading = false;
        if (!_target.CanPoll)
        {
            return;
        }

        var changed = _baseline is { } baseline ? value.ChangedKinds(baseline) : RepositoryChangeKinds.None;
        _baseline = value;
        var now = _time.GetTimestamp();
        if (changed != RepositoryChangeKinds.None)
        {
            _statusDueAt = now + ToTimestampTicks(_statusInterval);
            _target.Request(changed);
        }
        else if (now >= _statusDueAt)
        {
            _statusDueAt = now + ToTimestampTicks(_statusInterval);
            _target.Request(RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree);
        }
    }

    private long ToTimestampTicks(TimeSpan span) => (long)(span.TotalSeconds * _time.TimestampFrequency);
}
