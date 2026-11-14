namespace Gitunia.App.Tests.Fakes;

/// <summary>A clock tests move by hand. Timers made from it fire only when <see cref="Advance"/> passes them.</summary>
public sealed class ManualClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);

        return timer;
    }

    /// <summary>Moves the time on and runs the callback of every timer that came due, in due order.</summary>
    public void Advance(TimeSpan span)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + span;
        }

        while (TakeNextDue(target) is { } timer)
        {
            timer.Fire();
        }

        lock (_gate)
        {
            _now = target;
        }
    }

    private ManualTimer? TakeNextDue(DateTimeOffset target)
    {
        lock (_gate)
        {
            var next = _timers.Where(timer => timer.Due <= target).MinBy(timer => timer.Due);
            if (next is null)
            {
                return null;
            }

            _now = next.Due;
            next.Rearm();

            return next;
        }
    }

    private void Register(ManualTimer timer)
    {
        lock (_gate)
        {
            if (!_timers.Contains(timer))
            {
                _timers.Add(timer);
            }
        }
    }

    private void Unregister(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                clock.Unregister(this);

                return true;
            }

            _period = period;
            Due = clock.GetUtcNow() + dueTime;
            clock.Register(this);

            return true;
        }

        public void Rearm()
        {
            if (_period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero)
            {
                clock._timers.Remove(this);

                return;
            }

            Due += _period;
        }

        public void Fire() => callback(state);

        public void Dispose() => clock.Unregister(this);

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
