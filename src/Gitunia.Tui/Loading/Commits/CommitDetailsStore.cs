using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

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
    private readonly ICommitDetailsReader _reader;
    private readonly RepositoryLocation _location;
    private readonly Action<Action> _postToUi;
    private readonly TimeSpan _debounce;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeProvider _timeProvider;
    private readonly CommitDetailsCache _cache;
    private CancellationTokenSource? _pending;
    private CancellationTokenSource? _parentPending;
    private int _requestedParent;
    private long? _lastSelectTimestamp;

    /// <param name="reader">Reads the details of a commit.</param>
    /// <param name="location">The repository the commits belong to.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="debounce">How long a selection must stay put before the commit is read, when it follows another move closely.</param>
    /// <param name="delay">Waits out the debounce; tests inject one they control, the default is <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    /// <param name="cache">The cache of loaded details; the default is a <see cref="CommitDetailsCache"/> with its default bounds.</param>
    /// <param name="timeProvider">The clock that tells a fresh selection from one in a quick run of moves; tests inject one they control.</param>
    public CommitDetailsStore(
        ICommitDetailsReader reader,
        RepositoryLocation location,
        Action<Action> postToUi,
        TimeSpan debounce,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        CommitDetailsCache? cache = null,
        TimeProvider? timeProvider = null)
    {
        _delay = delay ?? Task.Delay;
        _cache = cache ?? new CommitDetailsCache();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _reader = reader;
        _location = location;
        _postToUi = postToUi;
        _debounce = debounce;
    }

    /// <summary>Raised on the UI thread when <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    public CommitDetailsState Current { get; private set; } = CommitDetailsState.Empty;

    /// <summary>The number of commits whose details are cached right now (bounded, see <see cref="CommitDetailsCache"/>).</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Makes <paramref name="commit"/> the selected commit and loads its details unless they are known already.</summary>
    /// <param name="commit">The commit to show.</param>
    /// <param name="prefetch">Commits likely to be selected next, most likely first; read in the background once the selection settled. Null or empty prefetches nothing.</param>
    public void Select(ObjectId commit, IReadOnlyList<ObjectId>? prefetch = null)
    {
        if (Current.Commit == commit)
        {
            return;
        }

        var now = _timeProvider.GetTimestamp();
        var isFresh = _lastSelectTimestamp is not { } last || _timeProvider.GetElapsedTime(last, now) >= _debounce;
        _lastSelectTimestamp = now;
        CancelPending();
        CancelParentPending();
        _requestedParent = 0;
        var wanted = WantedForPrefetch(commit, prefetch);
        if (_cache.TryGet(commit, out var known))
        {
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, known, string.Empty);
            Changed?.Invoke();
            StartPrefetch(wanted);
            return;
        }

        Current = new CommitDetailsState(commit, CommitDetailsStatus.Loading, null, string.Empty);
        Changed?.Invoke();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        var wait = isFresh ? TimeSpan.Zero : _debounce;
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

    /// <summary>
    /// Switches the loaded merge commit to its next parent (wrapping to the first): the files are read against that
    /// parent and replace <see cref="CommitDetailsState.Details"/>' file list when they arrive, with
    /// <see cref="CommitDetails.ParentIndex"/> set. The choice is per selected commit: selecting another starts at
    /// the first parent again, and the cache keeps the first-parent files.
    /// </summary>
    /// <returns>The parent index asked for, or null when nothing started (no loaded merge commit).</returns>
    public int? CycleParent()
    {
        if (Current is not
            { Status: CommitDetailsStatus.Loaded, Commit: { } commit, Details: { Parents.Count: > 1 } details }
            || details.IsComparison)
        {
            return null;
        }

        _requestedParent = (_requestedParent + 1) % details.Parents.Count;
        CancelParentPending();
        _parentPending = new CancellationTokenSource();
        var token = _parentPending.Token;
        var index = _requestedParent;
        _ = Task.Run(() => LoadParentAsync(commit, details, index, token), CancellationToken.None);
        return index;
    }

    /// <summary>
    /// Shows the difference between two commits in place of the selected commit's own changes (#535): the files are
    /// read as <c>git diff older newer</c> and replace <see cref="CommitDetailsState.Details"/>' file list when they
    /// arrive, with <see cref="CommitDetails.CompareOlder"/> and <see cref="CommitDetails.CompareNewer"/> set. The
    /// selected commit must be one of the two. Selecting another commit ends the comparison (the cache keeps the
    /// commit's own files); <see cref="ClearComparison"/> ends it without moving.
    /// </summary>
    /// <param name="older">The commit the diff starts from.</param>
    /// <param name="newer">The commit the diff ends at.</param>
    /// <returns>False when nothing started: the selected commit is not loaded or is neither of the two.</returns>
    public bool Compare(ObjectId older, ObjectId newer)
    {
        if (Current is not { Status: CommitDetailsStatus.Loaded, Commit: { } commit, Details: { } details }
            || (commit != older && commit != newer))
        {
            return false;
        }

        CancelParentPending();
        _parentPending = new CancellationTokenSource();
        var token = _parentPending.Token;
        _ = Task.Run(() => LoadComparisonAsync(commit, details, older, newer, token), CancellationToken.None);

        return true;
    }

    /// <summary>Goes back from a comparison to the selected commit's own changes.</summary>
    /// <returns>True when a comparison was shown or loading and is gone now.</returns>
    public bool ClearComparison()
    {
        var wasComparing = Current is { Details.IsComparison: true } || _parentPending is not null;
        if (!wasComparing)
        {
            return false;
        }

        CancelParentPending();
        if (Current is not { Details.IsComparison: true, Commit: { } commit })
        {
            return true;
        }

        Current = CommitDetailsState.Empty;
        Select(commit);

        return true;
    }

    public void Dispose()
    {
        CancelPending();
        CancelParentPending();
    }

    private void CancelParentPending()
    {
        _parentPending?.Cancel();
        _parentPending?.Dispose();
        _parentPending = null;
    }

    private async Task LoadParentAsync(ObjectId commit, CommitDetails details, int index, CancellationToken token)
    {
        try
        {
            var parent = details.Parents[index];
            var files = await _reader.ReadFilesAsync(_location, commit, parent, null, token).ConfigureAwait(false);
            var switched = details with { Files = files, ParentIndex = index };
            _postToUi(() => CompleteParent(commit, switched, token));
        }
        catch (OperationCanceledException)
        {
            // Another parent or commit was asked for.
        }
        catch (Exception)
        {
            // The files of the parent shown stay on screen; the next press tries again.
            _postToUi(() => ResetRequestedParent(commit, details.ParentIndex, token));
        }
    }

    private async Task LoadComparisonAsync(
        ObjectId commit,
        CommitDetails details,
        ObjectId older,
        ObjectId newer,
        CancellationToken token)
    {
        try
        {
            var files = await _reader.ReadFilesAsync(_location, newer, older, null, token).ConfigureAwait(false);
            var shown = details with { Files = files, CompareOlder = older, CompareNewer = newer };
            _postToUi(() => CompleteParent(commit, shown, token));
        }
        catch (OperationCanceledException)
        {
            // Another comparison or commit was asked for.
        }
        catch (Exception exception)
        {
            _postToUi(() => FailComparison(commit, exception.Message, token));
        }
    }

    private void FailComparison(ObjectId commit, string error, CancellationToken token)
    {
        if (token.IsCancellationRequested || Current.Commit != commit)
        {
            return;
        }

        Current = new CommitDetailsState(commit, CommitDetailsStatus.Failed, null, error);
        Changed?.Invoke();
    }

    private void ResetRequestedParent(ObjectId commit, int index, CancellationToken token)
    {
        if (!token.IsCancellationRequested && Current.Commit == commit)
        {
            _requestedParent = index;
        }
    }

    private void CompleteParent(ObjectId commit, CommitDetails details, CancellationToken token)
    {
        if (token.IsCancellationRequested || Current.Commit != commit)
        {
            return;
        }

        Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, details, string.Empty);
        Changed?.Invoke();
    }

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private ObjectId[] WantedForPrefetch(ObjectId commit, IReadOnlyList<ObjectId>? prefetch)
    {
        if (prefetch is null || prefetch.Count == 0)
        {
            return [];
        }

        return [.. prefetch.Where(candidate => candidate != commit && !_cache.Contains(candidate)).Distinct()];
    }

    private void StartPrefetch(ObjectId[] wanted)
    {
        if (wanted.Length == 0)
        {
            return;
        }

        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        _ = Task.Run(() => PrefetchAfterDebounceAsync(wanted, token), CancellationToken.None);
    }

    private async Task PrefetchAfterDebounceAsync(ObjectId[] wanted, CancellationToken token)
    {
        try
        {
            // The neighbours of a commit the user is only passing over are not worth a process each.
            await _delay(_debounce, token).ConfigureAwait(false);
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
                var details = await _reader.ReadAsync(_location, neighbour, cancellationToken: token).ConfigureAwait(false);
                if (details is not null)
                {
                    _postToUi(() => RememberPrefetched(details, token));
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
            await _delay(wait, token).ConfigureAwait(false);
            details = await _reader.ReadAsync(_location, commit, cancellationToken: token).ConfigureAwait(false);
            _postToUi(() => Complete(commit, details, string.Empty, token));
        }
        catch (OperationCanceledException)
        {
            // A newer selection (or the tab closing) made this load obsolete.
            return;
        }
        catch (Exception exception)
        {
            _postToUi(() => Complete(commit, null, exception.Message, token));
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
            _cache.Add(details);
            Current = new CommitDetailsState(commit, CommitDetailsStatus.Loaded, details, string.Empty);
        }

        Changed?.Invoke();
    }

    private void RememberPrefetched(CommitDetails details, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            _cache.Add(details);
        }
    }
}
