using Gitunia.Core.Diff;
using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>
/// The diff of the commit shown in the diff pane of one tab: the whole commit against its first parent (so merge commits
/// match the file list), read off the UI thread and flattened into a <see cref="DiffDocument"/>. Asking again for the same
/// commit costs nothing; asking for another one cancels the load in flight. UI thread only; results come back through
/// <c>postToUi</c>.
/// </summary>
public sealed class DiffStore(
    Repo repo,
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

    private readonly DiffCache cache = cache ?? new DiffCache();
    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;
    private CancellationTokenSource? pending;
    private CancellationTokenSource? prefetching;
    private IReadOnlyList<ObjectId> wanted = [];
    private ObjectId? firstParent;
    private bool isUnlimited;
    private bool isReleased;

    /// <summary>Raised on the UI thread when <see cref="Document"/> or <see cref="Status"/> changed.</summary>
    public event Action? Changed;

    public ObjectId? Commit { get; private set; }

    /// <summary>The loaded diff was cut short by the row or per-file limit, so <see cref="LoadAnyway"/> would show more.</summary>
    public bool IsCut { get; private set; }

    /// <summary>The number of diffs cached right now (bounded, see <see cref="DiffCache"/>).</summary>
    public int CachedCount => cache.Count;

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

        isUnlimited = false;
        isReleased = false;
        wanted = prefetch ?? [];
        if (cache.TryGet(commit, out var known))
        {
            CancelPending();
            CancelPrefetch();
            this.firstParent = firstParent;
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

        isUnlimited = true;
        Start(commit, firstParent);
        return true;
    }

    private void Start(ObjectId commit, ObjectId? firstParent)
    {
        CancelPending();
        CancelPrefetch();
        isReleased = false;
        this.firstParent = firstParent;
        Commit = commit;
        IsCut = false;
        Status = DiffStatus.Loading;
        Document = DiffDocument.Message("Loading...");
        Changed?.Invoke();
        pending = new CancellationTokenSource();
        var token = pending.Token;
        var limited = !isUnlimited;
        _ = Task.Run(() => LoadAsync(commit, firstParent, limited, token), CancellationToken.None);
    }

    /// <summary>Forgets the diff (the pane was closed), cancelling a load in flight.</summary>
    public void Clear()
    {
        CancelPending();
        CancelPrefetch();
        cache.Clear();
        wanted = [];
        Commit = null;
        firstParent = null;
        IsCut = false;
        Status = DiffStatus.None;
        Document = DiffDocument.Empty;
    }

    /// <summary>Drops the cached diffs and stops reading neighbours (the tab is not shown); the diff on show stays.</summary>
    public void ReleaseCache()
    {
        isReleased = true;
        CancelPrefetch();
        cache.Clear();
    }

    public void Dispose()
    {
        CancelPending();
        CancelPrefetch();
    }

    private void CancelPending()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
    }

    private static DiffDocument Present(DiffDocument document) =>
        document.Rows.Count == 0 ? DiffDocument.Message("No changes in this commit") : document;

    private void CancelPrefetch()
    {
        prefetching?.Cancel();
        prefetching?.Dispose();
        prefetching = null;
    }

    private void StartPrefetch()
    {
        var commits = wanted.Where(candidate => candidate != Commit && !cache.Contains(candidate)).Distinct().ToArray();
        if (commits.Length == 0)
        {
            return;
        }

        prefetching = new CancellationTokenSource();
        var token = prefetching.Token;
        _ = Task.Run(() => PrefetchAsync(commits, token), CancellationToken.None);
    }

    /// <summary>Reads the neighbours one after the other. Best effort: a cancelled, failed or oversized read is dropped, since the commit is loaded (and its error shown) when it is selected.</summary>
    private async Task PrefetchAsync(ObjectId[] commits, CancellationToken token)
    {
        try
        {
            await delay(PrefetchDelay, token).ConfigureAwait(false);
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
        var result = await repo.RunAsync(["rev-list", "--parents", "-n", "1", commit.ToString()], new GitRunOptions(ReadOnly: true), token).ConfigureAwait(false);
        var fields = result.StandardOutputText.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && ObjectId.TryParse(fields[1], out var parent) ? parent : null;
    }

    private void RememberPrefetched(ObjectId commit, DiffDocument document, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            cache.Add(commit, document);
        }
    }

    private async Task<DiffReadResult> ReadAsync(ObjectId commit, ObjectId? firstParent, int rows, int lineLimit, CancellationToken token)
    {
        var builder = new DiffDocumentBuilder();
        var warnings = new List<string>();
        var truncated = false;
        var fileCut = false;
        await foreach (var file in DiffReader.ReadCommitAsync(repo, commit, firstParent, token, warnings, lineLimit).ConfigureAwait(false))
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
            Document = DiffDocument.Message("Could not load the diff: " + error);
        }
        else
        {
            Status = DiffStatus.Loaded;
            IsCut = isCut;
            Document = Present(document);
            if (!isCut && !isReleased)
            {
                cache.Add(commit, document);
            }
        }

        Changed?.Invoke();
        if (Status == DiffStatus.Loaded && !isReleased)
        {
            StartPrefetch();
        }
    }
}
