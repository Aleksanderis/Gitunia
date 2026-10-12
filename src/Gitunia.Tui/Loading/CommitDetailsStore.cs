using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>
/// The details of the commit selected in one tab. <see cref="Select"/> is cheap and meant to be called on every
/// selection change: it shows the loading state at once, waits for the selection to settle (the debounce), cancels
/// whatever load the previous selection started and reads the commit off the UI thread. Results are cached, so
/// moving back over commits already seen costs no git process. UI thread only; results come back through
/// <c>postToUi</c>.
/// </summary>
public sealed class CommitDetailsStore : IDisposable
{
    private const int CacheCapacity = 64;

    private readonly Repo repo;
    private readonly Action<Action> postToUi;
    private readonly TimeSpan debounce;
    private readonly Dictionary<ObjectId, CommitDetails> cache = [];
    private readonly Queue<ObjectId> cacheOrder = new();
    private CancellationTokenSource? pending;

    /// <param name="repo">The repository the commits belong to.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="debounce">How long a selection must stay put before git is asked.</param>
    public CommitDetailsStore(Repo repo, Action<Action> postToUi, TimeSpan debounce)
    {
        this.repo = repo;
        this.postToUi = postToUi;
        this.debounce = debounce;
    }

    /// <summary>Raised on the UI thread when <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    public CommitDetailsState Current { get; private set; } = CommitDetailsState.Empty;

    /// <summary>Makes <paramref name="commit"/> the selected commit and loads its details unless they are known already.</summary>
    public void Select(ObjectId commit)
    {
        if (Current.Commit == commit)
        {
            return;
        }

        CancelPending();
        if (cache.TryGetValue(commit, out var known))
        {
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, known, string.Empty);
            Changed?.Invoke();
            return;
        }

        Current = new CommitDetailsState(commit, CommitDetailsStatus.Loading, null, string.Empty);
        Changed?.Invoke();
        pending = new CancellationTokenSource();
        var token = pending.Token;
        _ = Task.Run(() => LoadAsync(commit, token), CancellationToken.None);
    }

    /// <summary>Loads the selected commit again after its details failed to load. Does nothing in any other state.</summary>
    /// <returns>True when a new load started.</returns>
    public bool Retry()
    {
        if (Current is not { Status: CommitDetailsStatus.Failed, Commit: { } commit })
        {
            return false;
        }

        Current = CommitDetailsState.Empty;
        Select(commit);
        return true;
    }

    public void Dispose() => CancelPending();

    private void CancelPending()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
    }

    private async Task LoadAsync(ObjectId commit, CancellationToken token)
    {
        try
        {
            await Task.Delay(debounce, token).ConfigureAwait(false);
            var details = await CommitDetailsReader.ReadAsync(repo, commit, token).ConfigureAwait(false);
            postToUi(() => Complete(commit, details, string.Empty, token));
        }
        catch (OperationCanceledException)
        {
            // A newer selection (or the tab closing) made this load obsolete.
        }
        catch (Exception exception)
        {
            postToUi(() => Complete(commit, null, exception.Message, token));
        }
    }

    private void Complete(ObjectId commit, CommitDetails? details, string error, CancellationToken token)
    {
        // The token is cancelled the moment another commit is selected, even if this result was already queued.
        if (token.IsCancellationRequested || Current.Commit != commit)
        {
            return;
        }

        if (details is null)
        {
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Failed, null, error.Length > 0 ? error : "Commit not found");
        }
        else
        {
            Remember(commit, details);
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, details, string.Empty);
        }

        Changed?.Invoke();
    }

    private void Remember(ObjectId commit, CommitDetails details)
    {
        if (cache.TryAdd(commit, details))
        {
            cacheOrder.Enqueue(commit);
        }

        while (cacheOrder.Count > CacheCapacity)
        {
            cache.Remove(cacheOrder.Dequeue());
        }
    }
}
