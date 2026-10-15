namespace Gitunia.Tui.App.ExternalTools;

/// <summary>
/// Posts work to the UI thread for background producers (watchers, pollers, timers). While the terminal is handed
/// to an external tool there is no UI application to post to, so the work waits in a queue and runs, in order, on the
/// next application once the UI is back. Thread-safe.
/// </summary>
public sealed class SuspendableUiInvoker
{
    private readonly Lock _gate = new();
    private readonly Queue<Action> _waiting = new();
    private Action<Action> _target;
    private bool _isSuspended;

    public SuspendableUiInvoker(Action<Action> target)
    {
        _target = target;
    }

    public bool IsSuspended
    {
        get
        {
            lock (_gate)
            {
                return _isSuspended;
            }
        }
    }

    /// <summary>Posts the action now, or queues it while suspended.</summary>
    public void Invoke(Action action)
    {
        lock (_gate)
        {
            if (_isSuspended)
            {
                _waiting.Enqueue(action);

                return;
            }

            _target(action);
        }
    }

    /// <summary>From now on actions are queued; call before the application they were posted to is disposed.</summary>
    public void Suspend()
    {
        lock (_gate)
        {
            _isSuspended = true;
        }
    }

    /// <summary>Posts to <paramref name="target"/> from now on and posts everything that waited, in order.</summary>
    public void Resume(Action<Action> target)
    {
        lock (_gate)
        {
            _target = target;
            _isSuspended = false;
            while (_waiting.Count > 0)
            {
                target(_waiting.Dequeue());
            }
        }
    }
}
