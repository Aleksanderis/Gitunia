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
    private readonly SessionStore store;
    private readonly WorkspaceHost host;
    private readonly TimeSpan debounce;
    private readonly StatusChannel statuses;
    private readonly Action<Action> postToUi;
    private readonly Timer timer;
    private readonly Lock saveLock = new();
    private Workspace? lastSaved;
    private bool lastSaveFailed;

    /// <param name="statuses">Told when a save fails.</param>
    /// <param name="postToUi">Runs an action on the UI thread; saves run on a timer thread.</param>
    public SessionPersistence(SessionStore store, WorkspaceHost host, TimeSpan debounce, StatusChannel statuses, Action<Action> postToUi)
    {
        this.statuses = statuses;
        this.postToUi = postToUi;
        this.store = store;
        this.host = host;
        this.debounce = debounce;
        timer = new Timer(_ => Save(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Starts saving after every workspace change. The workspace as it is now counts as saved (it was just loaded).</summary>
    public void Start()
    {
        lastSaved = host.Workspace;
        host.Changed += OnChanged;
    }

    /// <summary>Saves now when something changed since the last save, for the moment the app exits.</summary>
    public void Flush()
    {
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Save();
    }

    public void Dispose()
    {
        host.Changed -= OnChanged;
        timer.Dispose();
    }

    private void OnChanged() => timer.Change(debounce, Timeout.InfiniteTimeSpan);

    private void Save()
    {
        lock (saveLock)
        {
            var snapshot = host.Workspace;
            if (ReferenceEquals(snapshot, lastSaved))
            {
                return;
            }

            if (store.SaveAsync(snapshot, CancellationToken.None).GetAwaiter().GetResult())
            {
                lastSaved = snapshot;
                lastSaveFailed = false;
            }
            else if (!lastSaveFailed)
            {
                // Once per failing streak: the next change retries silently, so the message does not repeat.
                lastSaveFailed = true;
                postToUi(() => statuses.Error("Could not save the session; open tabs will not be restored (retried on the next change)"));
            }
        }
    }
}
