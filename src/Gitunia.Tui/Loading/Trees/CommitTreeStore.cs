using Gitunia.Core.FileTree;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Trees;
using Gitunia.Tui.Loading.Commits;

namespace Gitunia.Tui.Loading.Trees;

/// <summary>
/// The file tree of the commit selected in one tab, loaded like <see cref="CommitDetailsStore"/>: <see
/// cref="Select"/> is cheap and meant for every selection change (it shows the loading state at once, cancels the
/// previous read and lists the tree off the UI thread; a selection that follows another closely waits for the
/// debounce, so a fast scroll starts no process per row). The last few trees are kept, as a tree of a commit never
/// changes. UI thread only; results come back through <c>postToUi</c>.
/// </summary>
public sealed class CommitTreeStore : IDisposable
{
    /// <summary>Trees kept for a quick return to a commit.</summary>
    public const int CacheCapacity = 4;

    private readonly ICommitTreeReader _reader;
    private readonly RepositoryLocation _location;
    private readonly Action<Action> _postToUi;
    private readonly TimeSpan _debounce;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeProvider _timeProvider;
    private readonly LinkedList<KeyValuePair<ObjectId, FileTreeModel>> _recentFirst = new();
    private CancellationTokenSource? _pending;
    private long? _lastSelectTimestamp;

    /// <param name="reader">Lists the tree of a commit.</param>
    /// <param name="location">The repository the commits belong to.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="debounce">How long a selection must stay put before the tree is read, when it follows another move
    /// closely.</param>
    /// <param name="delay">Waits out the debounce; tests inject one they control.</param>
    /// <param name="timeProvider">The clock that tells a fresh selection from one in a quick run of moves.</param>
    public CommitTreeStore(
        ICommitTreeReader reader,
        RepositoryLocation location,
        Action<Action> postToUi,
        TimeSpan debounce,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeProvider? timeProvider = null)
    {
        _reader = reader;
        _location = location;
        _postToUi = postToUi;
        _debounce = debounce;
        _delay = delay ?? Task.Delay;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Raised on the UI thread when <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    public CommitTreeState Current { get; private set; } = CommitTreeState.Empty;

    /// <summary>Makes <paramref name="commit"/> the selected commit and lists its tree unless that is known
    /// already.</summary>
    public void Select(ObjectId commit)
    {
        if (Current.Commit == commit)
        {
            return;
        }

        var now = _timeProvider.GetTimestamp();
        var isFresh = _lastSelectTimestamp is not { } last || _timeProvider.GetElapsedTime(last, now) >= _debounce;
        _lastSelectTimestamp = now;
        CancelPending();
        if (TryGetCached(commit) is { } known)
        {
            Current = new CommitTreeState(commit, CommitDetailsStatus.Loaded, known, string.Empty);
            Changed?.Invoke();

            return;
        }

        Current = new CommitTreeState(commit, CommitDetailsStatus.Loading, null, string.Empty);
        Changed?.Invoke();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        var wait = isFresh ? TimeSpan.Zero : _debounce;
        _ = Task.Run(() => LoadAsync(commit, wait, token), CancellationToken.None);
    }

    /// <summary>Lists the tree again after it failed to load. Does nothing in any other state.</summary>
    /// <returns>True when a new load started.</returns>
    public bool Retry()
    {
        if (Current is not { Status: CommitDetailsStatus.Failed, Commit: { } commit })
        {
            return false;
        }

        Current = CommitTreeState.Empty;
        Select(commit);

        return true;
    }

    public void Dispose() => CancelPending();

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private FileTreeModel? TryGetCached(ObjectId commit)
    {
        for (var node = _recentFirst.First; node is not null; node = node.Next)
        {
            if (node.Value.Key == commit)
            {
                _recentFirst.Remove(node);
                _recentFirst.AddFirst(node);

                return node.Value.Value;
            }
        }

        return null;
    }

    private async Task LoadAsync(ObjectId commit, TimeSpan wait, CancellationToken token)
    {
        try
        {
            await _delay(wait, token).ConfigureAwait(false);
            var entries = await _reader.ReadAsync(_location, commit, cancellationToken: token).ConfigureAwait(false);
            var model = FileTreeModel.Build(entries);
            _postToUi(() => Complete(commit, model, string.Empty, token));
        }
        catch (OperationCanceledException)
        {
            // A newer selection (or the tab closing) made this load obsolete.
        }
        catch (Exception exception)
        {
            _postToUi(() => Complete(commit, null, exception.Message, token));
        }
    }

    private void Complete(ObjectId commit, FileTreeModel? model, string error, CancellationToken token)
    {
        // The token is cancelled the moment another commit is selected, even if this result was already queued.
        if (token.IsCancellationRequested || Current.Commit != commit)
        {
            return;
        }

        if (model is null)
        {
            var message = error.Length > 0 ? error : "Tree not found";
            Current = new CommitTreeState(commit, CommitDetailsStatus.Failed, null, message);
        }
        else
        {
            Remember(commit, model);
            Current = new CommitTreeState(commit, CommitDetailsStatus.Loaded, model, string.Empty);
        }

        Changed?.Invoke();
    }

    private void Remember(ObjectId commit, FileTreeModel model)
    {
        _recentFirst.AddFirst(new KeyValuePair<ObjectId, FileTreeModel>(commit, model));
        while (_recentFirst.Count > CacheCapacity)
        {
            _recentFirst.RemoveLast();
        }
    }
}
