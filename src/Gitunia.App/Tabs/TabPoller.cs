using Gitunia.App.Tabs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App.Tabs;

/// <summary>
/// Notices changes made outside Gitunia without file watchers: while its tab is active it reads the repository
/// fingerprint every <see cref="PollOptions.RefsInterval"/> (off the UI thread) and asks for a full refresh when it
/// changed, else for a status refresh once the status interval has passed. Never overlaps itself, skips a tick while
/// the tab refreshes or runs an action, stops when the tab is hidden or disposed (a late result is dropped), and
/// lengthens the status interval after a slow refresh. All members except the timer callback run on the UI thread.
/// </summary>
public sealed class TabPoller : IDisposable
{
    private readonly RepositoryLocation _location;
    private readonly PollOptions _options;
    private readonly Action<Action> _postToUi;
    private readonly Func<bool> _canPoll;
    private readonly Action<RepositoryChangeKinds> _request;
    private readonly TimeProvider _time;
    private readonly Timer _timer;
    private int? _baseline;
    private long _statusDueAt;
    private TimeSpan _statusInterval;
    private int _epoch;
    private bool _isActive;
    private bool _isReading;
    private bool _isDisposed;

    public TabPoller(
        RepositoryLocation location,
        PollOptions options,
        Action<Action> postToUi,
        Func<bool> canPoll,
        Action<RepositoryChangeKinds> request,
        TimeProvider? time = null)
    {
        _location = location;
        _options = options;
        _postToUi = postToUi;
        _canPoll = canPoll;
        _request = request;
        _time = time ?? TimeProvider.System;
        _statusInterval = options.StatusInterval;
        _timer = new Timer(_ => _postToUi(Tick));
    }

    /// <summary>The wait between status refreshes right now (above the configured one after a slow refresh).</summary>
    public TimeSpan StatusInterval => _statusInterval;

    /// <summary>Takes the first fingerprint, so a change made while the tab loads is seen by the first tick.</summary>
    public void Start() => _ = Task.Run(ReadBaseline, CancellationToken.None);

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

        _statusDueAt = _time.GetTimestamp();
        Tick();
    }

    /// <summary>
    /// A refresh without a graph reload took <paramref name="took"/>: the next status refresh waits accordingly.
    /// </summary>
    public void RecordRefresh(TimeSpan took)
    {
        _statusInterval = _options.StatusIntervalAfter(took);
        _statusDueAt = _time.GetTimestamp() + ToTimestampTicks(_statusInterval);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _epoch++;
        _timer.Dispose();
    }

    private void ReadBaseline()
    {
        var value = RepositoryFingerprint.Compute(_location);
        _postToUi(() => _baseline ??= value);
    }

    private void Tick()
    {
        if (_isDisposed || !_isActive || _isReading || !_canPoll())
        {
            return;
        }

        _isReading = true;
        var epoch = _epoch;
        _ = Task.Run(() => ReadFingerprint(epoch), CancellationToken.None);
    }

    private void ReadFingerprint(int epoch)
    {
        var value = RepositoryFingerprint.Compute(_location);
        _postToUi(() => Compare(value, epoch));
    }

    private void Compare(int value, int epoch)
    {
        if (epoch != _epoch)
        {
            // Hidden, shown again or disposed meanwhile: the new epoch has its own read.
            return;
        }

        _isReading = false;
        if (!_canPoll())
        {
            return;
        }

        var changed = _baseline is { } baseline && baseline != value;
        _baseline = value;
        var now = _time.GetTimestamp();
        if (changed)
        {
            _statusDueAt = now + ToTimestampTicks(_statusInterval);
            _request(RepositoryChangeKinds.All);
        }
        else if (now >= _statusDueAt)
        {
            _statusDueAt = now + ToTimestampTicks(_statusInterval);
            _request(RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree);
        }
    }

    private long ToTimestampTicks(TimeSpan span) => (long)(span.TotalSeconds * _time.TimestampFrequency);
}
