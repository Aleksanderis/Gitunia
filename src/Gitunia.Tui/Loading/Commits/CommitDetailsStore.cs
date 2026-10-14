using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories;

namespace Gitunia.Tui.Loading.Commits;

/// <summary>
/// The details of the commit selected in one tab. <see cref="Select"/> is cheap and meant to be called on every
/// selection change: it shows the loading state at once, cancels whatever load (or prefetch) the previous selection
/// started and reads the commit off the UI thread. A selection that follows a pause is read at once; one that follows
/// another move closely (holding a key) waits for the debounce, so a fast scroll starts no process per row. Once the
/// selection has settled, the commits around it are read in the background into a bounded LRU cache
/// (<see cref="CommitDetailsCache"/>), so stepping to a neighbour finds its details ready. UI thread only; results come
/// back through <c>postToUi</c>.
/// </summary>
public sealed class CommitDetailsStore : IDisposable
{
    private readonly Repository repository;
    private readonly Action<Action> postToUi;
    private readonly TimeSpan debounce;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly TimeProvider timeProvider;
    private readonly CommitDetailsCache cache;
    private CancellationTokenSource? pending;
    private long? lastSelectTimestamp;

    /// <param name="repo">The repository the commits belong to.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="debounce">How long a selection must stay put before the commit is read, when it follows another move closely.</param>
    /// <param name="delay">Waits out the debounce; tests inject one they control, the default is <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    /// <param name="cache">The cache of loaded details; the default is a <see cref="CommitDetailsCache"/> with its default bounds.</param>
    /// <param name="timeProvider">The clock that tells a fresh selection from one in a quick run of moves; tests inject one they control.</param>
    public CommitDetailsStore(
        Repository repository,
        Action<Action> postToUi,
        TimeSpan debounce,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        CommitDetailsCache? cache = null,
        TimeProvider? timeProvider = null)
    {
        this.delay = delay ?? Task.Delay;
        this.cache = cache ?? new CommitDetailsCache();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.repository = repository;
        this.postToUi = postToUi;
        this.debounce = debounce;
    }

    /// <summary>Raised on the UI thread when <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    public CommitDetailsState Current { get; private set; } = CommitDetailsState.Empty;

    /// <summary>The number of commits whose details are cached right now (bounded, see <see cref="CommitDetailsCache"/>).</summary>
    public int CachedCount => cache.Count;

    /// <summary>Makes <paramref name="commit"/> the selected commit and loads its details unless they are known already.</summary>
    /// <param name="commit">The commit to show.</param>
    /// <param name="prefetch">Commits likely to be selected next, most likely first; read in the background once the selection settled. Null or empty prefetches nothing.</param>
    public void Select(ObjectId commit, IReadOnlyList<ObjectId>? prefetch = null)
    {
        if (Current.Commit == commit)
        {
            return;
        }

        var now = timeProvider.GetTimestamp();
        var isFresh = lastSelectTimestamp is not { } last || timeProvider.GetElapsedTime(last, now) >= debounce;
        lastSelectTimestamp = now;
        CancelPending();
        var wanted = WantedForPrefetch(commit, prefetch);
        if (cache.TryGet(commit, out var known))
        {
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, known, string.Empty);
            Changed?.Invoke();
            StartPrefetch(wanted);
            return;
        }

        Current = new CommitDetailsState(commit, CommitDetailsStatus.Loading, null, string.Empty);
        Changed?.Invoke();
        pending = new CancellationTokenSource();
        var token = pending.Token;
        var wait = isFresh ? TimeSpan.Zero : debounce;
        _ = Task.Run(() => LoadAsync(commit, wait, wanted, token), CancellationToken.None);
    }

    /// <summary>Loads the selected commit again after its details failed to load. Does nothing in any other state.</summary>
    /// <returns>True when a new load started (without prefetching neighbours: the next move does that).</returns>
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

    private ObjectId[] WantedForPrefetch(ObjectId commit, IReadOnlyList<ObjectId>? prefetch)
    {
        if (prefetch is null || prefetch.Count == 0)
        {
            return [];
        }

        return prefetch.Where(candidate => candidate != commit && !cache.Contains(candidate)).Distinct().ToArray();
    }

    private void StartPrefetch(ObjectId[] wanted)
    {
        if (wanted.Length == 0)
        {
            return;
        }

        pending = new CancellationTokenSource();
        var token = pending.Token;
        _ = Task.Run(() => PrefetchAfterDebounceAsync(wanted, token), CancellationToken.None);
    }

    private async Task PrefetchAfterDebounceAsync(ObjectId[] wanted, CancellationToken token)
    {
        try
        {
            // The neighbours of a commit the user is only passing over are not worth a process each.
            await delay(debounce, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await PrefetchAsync(wanted, token).ConfigureAwait(false);
    }

    /// <summary>Reads the neighbours one after the other. Best effort: a cancelled or failed read is dropped, since the commit is loaded (and its error shown) when it is selected.</summary>
    private async Task PrefetchAsync(ObjectId[] wanted, CancellationToken token)
    {
        foreach (var neighbour in wanted)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var details = await CommitDetailsReader.ReadAsync(repository, neighbour, token).ConfigureAwait(false);
                if (details is not null)
                {
                    postToUi(() => RememberPrefetched(details, token));
                }
            }
            catch (OperationCanceledException)
            {
                // Another commit was selected, so these neighbours are not the ones that matter any more.
                return;
            }
            catch (Exception)
            {
                // Dropped on purpose, see the summary; the other neighbours are still worth reading.
            }
        }
    }

    private async Task LoadAsync(ObjectId commit, TimeSpan wait, ObjectId[] wanted, CancellationToken token)
    {
        CommitDetails? details;
        try
        {
            await delay(wait, token).ConfigureAwait(false);
            details = await CommitDetailsReader.ReadAsync(repository, commit, token).ConfigureAwait(false);
            postToUi(() => Complete(commit, details, string.Empty, token));
        }
        catch (OperationCanceledException)
        {
            // A newer selection (or the tab closing) made this load obsolete.
            return;
        }
        catch (Exception exception)
        {
            postToUi(() => Complete(commit, null, exception.Message, token));
            return;
        }

        if (details is not null && wanted.Length > 0)
        {
            await PrefetchAfterDebounceAsync(wanted, token).ConfigureAwait(false);
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
            cache.Add(details);
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, details, string.Empty);
        }

        Changed?.Invoke();
    }

    private void RememberPrefetched(CommitDetails details, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            cache.Add(details);
        }
    }
}
