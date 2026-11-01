using Gitunia.Core.Persistence;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>
/// Saves the workspace and the commit drafts of its tabs to the session file after changes (debounced, off the UI thread) and once more on exit.
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
    private readonly Func<IReadOnlyDictionary<TabId, SessionDraft>> _captureDrafts;
    private Workspace? _lastSaved;
    private IReadOnlyDictionary<TabId, SessionDraft> _lastSavedDrafts = new Dictionary<TabId, SessionDraft>();
    private IReadOnlyDictionary<TabId, SessionDraft> _drafts = new Dictionary<TabId, SessionDraft>();
    private bool _lastSaveFailed;

    /// <param name="statuses">Told when a save fails.</param>
    /// <param name="postToUi">Runs an action on the UI thread; saves run on a timer thread.</param>
    /// <param name="captureDrafts">Reads the commit drafts of the tabs; called on the UI thread only (null keeps none).</param>
    public SessionPersistence(
        SessionStore store,
        WorkspaceHost host,
        TimeSpan debounce,
        StatusChannel statuses,
        Action<Action> postToUi,
        Func<IReadOnlyDictionary<TabId, SessionDraft>>? captureDrafts = null)
    {
        _captureDrafts = captureDrafts ?? (() => new Dictionary<TabId, SessionDraft>());
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
        _drafts = _lastSavedDrafts = _captureDrafts();
        _host.Changed += OnChanged;
    }

    /// <summary>Saves now when something changed since the last save, for the moment the app exits.</summary>
    public void Flush()
    {
        CaptureDrafts();
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Save();
    }

    /// <summary>Saves now like <see cref="Flush"/> and tells whether the session file is up to date (false: the last save failed).</summary>
    public bool FlushSucceeded()
    {
        Flush();
        return !_lastSaveFailed;
    }

    /// <summary>Tells that a commit draft changed (UI thread); the session is saved after the debounce.</summary>
    public void NotifyDraftChanged() => OnChanged();

    public void Dispose()
    {
        _host.Changed -= OnChanged;
        _timer.Dispose();
    }

    private void OnChanged()
    {
        CaptureDrafts();
        _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
    }

    // The drafts are mutable UI objects: copy them here, on the UI thread, and let the timer thread save the copy.
    private void CaptureDrafts()
    {
        Volatile.Write(ref _drafts, _captureDrafts());
    }

    private static bool SameDrafts(IReadOnlyDictionary<TabId, SessionDraft> left, IReadOnlyDictionary<TabId, SessionDraft> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var other) && other == pair.Value);

    private void Save()
    {
        lock (_saveLock)
        {
            var snapshot = _host.Workspace;
            var drafts = Volatile.Read(ref _drafts);
            if (ReferenceEquals(snapshot, _lastSaved) && SameDrafts(drafts, _lastSavedDrafts))
            {
                return;
            }

            if (_store.SaveAsync(snapshot, drafts, CancellationToken.None).GetAwaiter().GetResult())
            {
                _lastSaved = snapshot;
                _lastSavedDrafts = drafts;
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
