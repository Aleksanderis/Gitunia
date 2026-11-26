using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Sync;

/// <summary>
/// The background fetch of the active tab (ux-workflows.md, workflow 5). A slow timer asks <see cref="Tick"/> whether the
/// shown tab is due; if so it dispatches <see cref="SyncRequested"/> with <see cref="SyncKind.AutoFetch"/> and the sync
/// reducer decides whether it starts (nothing else running in the tab). Only the active tab fetches, nothing runs while
/// the terminal is unfocused, and a tab that is switched to after a long time is due at the next tick. The waits and
/// the backoff are <see cref="AutoFetchSchedule"/>; the results of fetches come in through <see cref="OnFetchEnded"/>.
/// </summary>
public sealed class AutoFetchScheduler : IDisposable
{
    /// <summary>How often the timer asks whether the active tab is due; no git runs for the question.</summary>
    public static readonly TimeSpan TickPeriod = TimeSpan.FromSeconds(15);

    private readonly Dispatcher _host;
    private readonly IUiInvoker _ui;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ShutdownTasks _tasks = new();
    private readonly Dictionary<TabId, AutoFetchSchedule> _schedules = [];
    private readonly Lock _gate = new();
    private DateTimeOffset _baseline;
    private TimeSpan _interval = TimeSpan.FromMinutes(5);
    private bool _isEnabled;
    private bool _isFocused = true;
    private bool _started;

    public AutoFetchScheduler(Dispatcher host, IUiInvoker ui, TimeProvider? time = null)
    {
        _host = host;
        _ui = ui;
        _time = time ?? TimeProvider.System;
        _baseline = _time.GetUtcNow();
    }

    /// <summary>Starts the timer. Calling it twice has no effect.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        var token = _cancellation.Token;
        _tasks.Run(() => RunAsync(token));
    }

    /// <summary>Takes the options of the config file: every tab waits a full interval again, a stopped tab resumes.</summary>
    /// <param name="isEnabled">The <c>autoFetch</c> option.</param>
    /// <param name="interval">The <c>autoFetchMinutes</c> option as a time span.</param>
    public void Configure(bool isEnabled, TimeSpan interval)
    {
        lock (_gate)
        {
            var changed = isEnabled != _isEnabled || interval != _interval;
            _isEnabled = isEnabled;
            _interval = interval;
            if (changed)
            {
                _schedules.Clear();
                _baseline = _time.GetUtcNow();
            }
        }
    }

    /// <summary>The terminal gained or lost focus; on gaining it, a tab that is overdue fetches at once.</summary>
    /// <param name="isFocused">True when the terminal has focus.</param>
    public void SetFocused(bool isFocused)
    {
        _isFocused = isFocused;
        if (isFocused)
        {
            Tick();
        }
    }

    /// <summary>A fetch of any tab ended: its schedule starts over, backs off or stops. Any thread.</summary>
    /// <param name="ended">What happened.</param>
    public void OnFetchEnded(FetchEnded ended)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            _schedules[ended.TabId] = ScheduleOf(ended.TabId).Ended(ended, now);
        }
    }

    /// <summary>Asks for a fetch of the active tab when it is due. UI thread only.</summary>
    public void Tick()
    {
        if (!_isEnabled || !_isFocused || _cancellation.IsCancellationRequested)
        {
            return;
        }

        if (_host.Workspace.ActiveTab is not { } tab || !CanFetch(tab))
        {
            return;
        }

        var tabId = tab.Id;

        var now = _time.GetUtcNow();
        lock (_gate)
        {
            var schedule = ScheduleOf(tabId);
            if (!schedule.IsDue(now, _interval))
            {
                return;
            }

            _schedules[tabId] = schedule.Started(now);
        }

        _host.Dispatch(new SyncRequested(tabId, SyncKind.AutoFetch));
    }

    /// <summary>Waits until the timer has ended after <see cref="Dispose"/> or the timeout has passed.</summary>
    /// <param name="timeout">The longest wait.</param>
    public bool WaitForStop(TimeSpan timeout) => _tasks.Wait(timeout);

    public void Dispose() => _cancellation.Cancel();

    // A tab that is loading, missing or busy is left alone; the reducer also refuses a busy tab, but then the round would be used up.
    private static bool CanFetch(RepositoryState tab) =>
        !tab.IsMissing
        && !tab.Refs.IsEmpty
        && tab.Sync is null
        && !tab.Running.IsBusy;

    // A tab the timer has not seen waits from the last configuration, so one opened after a long time is due at once.
    private AutoFetchSchedule ScheduleOf(TabId tabId)
    {
        if (!_schedules.TryGetValue(tabId, out var schedule))
        {
            schedule = AutoFetchSchedule.StartingAt(_baseline);
            _schedules[tabId] = schedule;
        }

        return schedule;
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TickPeriod, _time);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                _ui.Post(Tick);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }
}
