using Gitunia.Core.Persistence;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>
/// Saves the workspace to the session file after changes (debounced, off the UI thread) and once more on exit.
/// A failed save never disturbs the app; the next change tries again.
/// </summary>
public sealed class SessionPersistence : IDisposable
{
    private readonly SessionStore _store;
    private readonly WorkspaceHost _host;
    private readonly TimeSpan _debounce;
    private readonly StatusChannel _statuses;
    private readonly Action<Action> _postToUi;
    private readonly Timer _timer;
    private readonly Lock _saveLock = new();
    private Workspace? _lastSaved;
    private bool _lastSaveFailed;

    /// <param name="statuses">Told when a save fails.</param>
    /// <param name="postToUi">Runs an action on the UI thread; saves run on a timer thread.</param>
    public SessionPersistence(SessionStore store, WorkspaceHost host, TimeSpan debounce, StatusChannel statuses, Action<Action> postToUi)
    {
        _statuses = statuses;
        _postToUi = postToUi;
        _store = store;
        _host = host;
        _debounce = debounce;
        _timer = new Timer(_ => Save(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Starts saving after every workspace change. The workspace as it is now counts as saved (it was just loaded).</summary>
    public void Start()
    {
        _lastSaved = _host.Workspace;
        _host.Changed += OnChanged;
    }

    /// <summary>Saves now when something changed since the last save, for the moment the app exits.</summary>
    public void Flush()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Save();
    }

    public void Dispose()
    {
        _host.Changed -= OnChanged;
        _timer.Dispose();
    }

    private void OnChanged() => _timer.Change(_debounce, Timeout.InfiniteTimeSpan);

    private void Save()
    {
        lock (_saveLock)
        {
            var snapshot = _host.Workspace;
            if (ReferenceEquals(snapshot, _lastSaved))
            {
                return;
            }

            if (_store.SaveAsync(snapshot, CancellationToken.None).GetAwaiter().GetResult())
            {
                _lastSaved = snapshot;
                _lastSaveFailed = false;
            }
            else if (!_lastSaveFailed)
            {
                // Once per failing streak: the next change retries silently, so the message does not repeat.
                _lastSaveFailed = true;
                _postToUi(() => _statuses.Error("Could not save the session; open tabs will not be restored (retried on the next change)"));
            }
        }
    }
}
