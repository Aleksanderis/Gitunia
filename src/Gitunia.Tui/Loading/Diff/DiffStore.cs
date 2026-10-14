using Gitunia.Core.Diff;
using Gitunia.Git;
using Gitunia.Git.Diff;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;

namespace Gitunia.Tui.Loading.Diff;

/// <summary>
/// The diff of the commit shown in the diff pane of one tab: the whole commit against its first parent (so merge commits
/// match the file list), read off the UI thread and flattened into a <see cref="DiffDocument"/>. Asking again for the same
/// commit costs nothing; asking for another one cancels the load in flight. UI thread only; results come back through
/// <c>postToUi</c>.
/// </summary>
public sealed class DiffStore(
    Repository repository,
    Action<Action> postToUi,
    int rowLimit = DiffStore.DefaultRowLimit,
    int fileLineLimit = DiffStore.DefaultFileLineLimit,
    DiffCache? cache = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : IDisposable
{
    /// <summary>Rows kept before the diff is cut short; the pane then says so in a last row.</summary>
    public const int DefaultRowLimit = 400_000;

    /// <summary>Hunk lines kept per file before the file is cut short.</summary>
    public const int DefaultFileLineLimit = 100_000;

    /// <summary>Rows a prefetched diff may have; a bigger one is left to be loaded when it is selected, so a background read never builds a huge document.</summary>
    public const int PrefetchRowLimit = 4_000;

    /// <summary>How long the shown diff must stay put before the neighbours are read, so passing over a commit starts no process.</summary>
    public static readonly TimeSpan PrefetchDelay = TimeSpan.FromMilliseconds(80);

    private readonly DiffCache _cache = cache ?? new DiffCache();
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private CancellationTokenSource? _pending;
    private CancellationTokenSource? _prefetching;
    private IReadOnlyList<ObjectId> _wanted = [];
    private ObjectId? _firstParent;
    private bool _isUnlimited;
    private bool _isReleased;

    /// <summary>Raised on the UI thread when <see cref="Document"/> or <see cref="Status"/> changed.</summary>
    public event Action? Changed;

    public ObjectId? Commit { get; private set; }

    /// <summary>The loaded diff was cut short by the row or per-file limit, so <see cref="LoadAnyway"/> would show more.</summary>
    public bool IsCut { get; private set; }

    /// <summary>The number of diffs cached right now (bounded, see <see cref="DiffCache"/>).</summary>
    public int CachedCount => _cache.Count;

    public DiffStatus Status { get; private set; } = DiffStatus.None;

    /// <summary>The rows to show: a remark while loading or after a failure, the diff once loaded.</summary>
    public DiffDocument Document { get; private set; } = DiffDocument.Empty;

    /// <summary>Starts loading the diff of <paramref name="commit"/> unless it is the one shown (or loading) already.</summary>
    /// <param name="commit">The commit.</param>
    /// <param name="firstParent">The first parent to compare against, null for a root commit.</param>
    /// <param name="prefetch">Commits likely to be selected next, most likely first; their diffs are read in the background into the cache once this one is shown. Null or empty prefetches nothing.</param>
    /// <returns>True when a load was started or the diff came from the cache.</returns>
    public bool Load(ObjectId commit, ObjectId? firstParent, IReadOnlyList<ObjectId>? prefetch = null)
    {
        if (Commit == commit && Status != DiffStatus.Failed)
        {
            return false;
        }

        _isUnlimited = false;
        _isReleased = false;
        _wanted = prefetch ?? [];
        if (_cache.TryGet(commit, out var known))
        {
            CancelPending();
            CancelPrefetch();
            _firstParent = firstParent;
            Commit = commit;
            IsCut = false;
            Status = DiffStatus.Loaded;
            Document = Present(known);
            Changed?.Invoke();
            StartPrefetch();
            return true;
        }

        Start(commit, firstParent);
        return true;
    }

    /// <summary>Loads the shown commit again without the row and per-file limits.</summary>
    /// <returns>False when nothing is shown or nothing was cut.</returns>
    public bool LoadAnyway()
    {
        if (Commit is not { } commit || !IsCut || Status != DiffStatus.Loaded)
        {
            return false;
        }

        _isUnlimited = true;
        Start(commit, _firstParent);
        return true;
    }

    private void Start(ObjectId commit, ObjectId? firstParent)
    {
        CancelPending();
        CancelPrefetch();
        _isReleased = false;
        _firstParent = firstParent;
        Commit = commit;
        IsCut = false;
        Status = DiffStatus.Loading;
        Document = DiffDocument.Message("Loading...");
        Changed?.Invoke();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        var limited = !_isUnlimited;
        _ = Task.Run(() => LoadAsync(commit, firstParent, limited, token), CancellationToken.None);
    }

    /// <summary>Forgets the diff (the pane was closed), cancelling a load in flight.</summary>
    public void Clear()
    {
        CancelPending();
        CancelPrefetch();
        _cache.Clear();
        _wanted = [];
        Commit = null;
        _firstParent = null;
        IsCut = false;
        Status = DiffStatus.None;
        Document = DiffDocument.Empty;
    }

    /// <summary>Drops the cached diffs and stops reading neighbours (the tab is not shown); the diff on show stays.</summary>
    public void ReleaseCache()
    {
        _isReleased = true;
        CancelPrefetch();
        _cache.Clear();
    }

    public void Dispose()
    {
        CancelPending();
        CancelPrefetch();
    }

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private static DiffDocument Present(DiffDocument document) =>
        document.Rows.Count == 0 ? DiffDocument.Message("No changes in this commit") : document;

    private void CancelPrefetch()
    {
        _prefetching?.Cancel();
        _prefetching?.Dispose();
        _prefetching = null;
    }

    private void StartPrefetch()
    {
        var commits = _wanted.Where(candidate => candidate != Commit && !_cache.Contains(candidate)).Distinct().ToArray();
        if (commits.Length == 0)
        {
            return;
        }

        _prefetching = new CancellationTokenSource();
        var token = _prefetching.Token;
        _ = Task.Run(() => PrefetchAsync(commits, token), CancellationToken.None);
    }

    /// <summary>Reads the neighbours one after the other. Best effort: a cancelled, failed or oversized read is dropped, since the commit is loaded (and its error shown) when it is selected.</summary>
    private async Task PrefetchAsync(ObjectId[] commits, CancellationToken token)
    {
        try
        {
            await _delay(PrefetchDelay, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        foreach (var commit in commits)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var parent = await FirstParentOfAsync(commit, token).ConfigureAwait(false);
                var document = await ReadAsync(commit, parent, PrefetchRowLimit, Math.Min(fileLineLimit, PrefetchRowLimit), token).ConfigureAwait(false);
                if (document is { IsCut: false })
                {
                    postToUi(() => RememberPrefetched(commit, document.Document, token));
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

    private async Task<ObjectId?> FirstParentOfAsync(ObjectId commit, CancellationToken token)
    {
        var result = await repository.RunAsync(["rev-list", "--parents", "-n", "1", commit.ToString()], GitRunOptions.ReadOnlyRun, token).ConfigureAwait(false);
        var fields = result.StandardOutputText.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && ObjectId.TryParse(fields[1], out var parent) ? parent : null;
    }

    private void RememberPrefetched(ObjectId commit, DiffDocument document, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            _cache.Add(commit, document);
        }
    }

    private async Task<DiffReadResult> ReadAsync(ObjectId commit, ObjectId? firstParent, int rows, int lineLimit, CancellationToken token)
    {
        var builder = new DiffDocumentBuilder();
        var warnings = new List<string>();
        var truncated = false;
        var fileCut = false;
        await foreach (var file in DiffReader.ReadCommitAsync(repository, commit, firstParent, warnings, lineLimit, token).ConfigureAwait(false))
        {
            builder.Add(file);
            fileCut |= file.IsTruncated;
            if (builder.RowCount >= rows)
            {
                // Leaving the loop disposes the stream, which stops git.
                truncated = true;
                break;
            }
        }

        if (truncated)
        {
            builder.AddTrailingNote($"Diff cut short after {rows} rows (L loads all of it)");
        }

        return new DiffReadResult(builder.Build(warnings), truncated || fileCut);
    }

    private async Task LoadAsync(ObjectId commit, ObjectId? firstParent, bool limited, CancellationToken token)
    {
        try
        {
            var read = await ReadAsync(commit, firstParent, limited ? rowLimit : int.MaxValue, limited ? fileLineLimit : int.MaxValue, token).ConfigureAwait(false);
            postToUi(() => Complete(commit, read.Document, string.Empty, read.IsCut, token));
        }
        catch (OperationCanceledException)
        {
            // Another commit was asked for (or the tab closed).
        }
        catch (Exception exception)
        {
            postToUi(() => Complete(commit, null, exception.Message, false, token));
        }
    }

    private void Complete(ObjectId commit, DiffDocument? document, string error, bool isCut, CancellationToken token)
    {
        if (token.IsCancellationRequested || Commit != commit)
        {
            return;
        }

        if (document is null)
        {
            Status = DiffStatus.Failed;
            Document = DiffDocument.Message($"Could not load the diff: {error}");
        }
        else
        {
            Status = DiffStatus.Loaded;
            IsCut = isCut;
            Document = Present(document);
            if (!isCut && !_isReleased)
            {
                _cache.Add(commit, document);
            }
        }

        Changed?.Invoke();
        if (Status == DiffStatus.Loaded && !_isReleased)
        {
            StartPrefetch();
        }
    }
}
