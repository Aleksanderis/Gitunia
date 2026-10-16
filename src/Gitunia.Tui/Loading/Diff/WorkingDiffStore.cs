using Gitunia.Core.Diff;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Loading.Diff;

/// <summary>
/// The diff of the file under the cursor of the working-tree view of one tab, read off the UI thread and flattened into a
/// <see cref="DiffDocument"/>. Asking again for the same file in the same status generation costs nothing; asking for another
/// one cancels the load in flight. A new generation (the status was read again, so the file may have changed even though its status
/// letters did not) drops the cache and reloads the shown file; while it loads the old rows stay, so a refresh never blanks the pane.
/// UI thread only; results come back through <c>postToUi</c>.
/// </summary>
public sealed class WorkingDiffStore(
    IDiffReader reader,
    RepositoryLocation location,
    Action<Action> postToUi,
    int rowLimit = DiffStore.DefaultRowLimit,
    int fileLineLimit = DiffStore.DefaultFileLineLimit) : IDisposable
{
    private readonly KeyedDiffCache<WorkingDiffRequest> _cache = new();
    private CancellationTokenSource? _pending;
    private int _generation;
    private bool _isUnlimited;

    /// <summary>Raised on the UI thread when <see cref="Document"/> or <see cref="Status"/> changed.</summary>
    public event Action? Changed;

    /// <summary>The file shown (or loading), null when none is asked for.</summary>
    public WorkingDiffRequest? Request { get; private set; }

    /// <summary>The loaded diff was cut short by the row or per-file limit, so <see cref="LoadAnyway"/> would show more.</summary>
    public bool IsCut { get; private set; }

    public DiffStatus Status { get; private set; } = DiffStatus.None;

    /// <summary>How diffs are read (context, whole files, whitespace); changed with <see cref="SetOptions"/>.</summary>
    public DiffOptions Options { get; private set; } = DiffOptions.Default;

    /// <summary>The rows to show: a remark while there is nothing to show, while loading the first time or after a failure, the diff once loaded.</summary>
    public DiffDocument Document { get; private set; } = DiffDocument.Empty;

    /// <summary>The number of diffs cached right now.</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Shows a remark instead of a diff (nothing selected, the status not loaded), cancelling a load in flight.</summary>
    public void ShowNothing(string text)
    {
        if (Request is null && Status == DiffStatus.None && Document.Rows is [{ Text: var shown }] && shown == text)
        {
            return;
        }

        CancelPending();
        Request = null;
        IsCut = false;
        Status = DiffStatus.None;
        Document = DiffDocument.Message(text);
        Changed?.Invoke();
    }

    /// <summary>Starts loading the diff of <paramref name="request"/> unless it is the one shown (or loading) in this generation.</summary>
    /// <param name="request">The file and side.</param>
    /// <param name="generation">Changes whenever the status was read again; a different value drops the cache and reloads.</param>
    /// <returns>True when a load was started or the diff came from the cache.</returns>
    public bool Show(WorkingDiffRequest request, int generation)
    {
        var isNewGeneration = generation != _generation;
        if (isNewGeneration)
        {
            _generation = generation;
            _cache.Clear();
        }

        // A failed read is not retried until the status is read again, the options change or L asks: a key press must not start git again.
        if (request == Request && !isNewGeneration)
        {
            return false;
        }

        _isUnlimited = false;
        if (_cache.TryGet(request, out var known))
        {
            CancelPending();
            Request = request;
            IsCut = false;
            Status = DiffStatus.Loaded;
            Document = Present(known);
            Changed?.Invoke();
            return true;
        }

        Start(request, keepRows: request == Request && Status == DiffStatus.Loaded);
        return true;
    }

    /// <summary>
    /// Reads diffs with other options from now on: the cache is dropped (it holds diffs read the old way) and the shown file
    /// loads again.
    /// </summary>
    /// <returns>False when the options did not change.</returns>
    public bool SetOptions(DiffOptions options)
    {
        if (options == Options)
        {
            return false;
        }

        Options = options;
        _cache.Clear();
        if (Request is { } request)
        {
            Start(request, keepRows: false);
        }

        return true;
    }

    /// <summary>Loads the shown file again without the row and per-file limits.</summary>
    /// <returns>False when nothing is shown or nothing was cut.</returns>
    public bool LoadAnyway()
    {
        if (Request is not { } request || !IsCut || Status != DiffStatus.Loaded)
        {
            return false;
        }

        _isUnlimited = true;
        Start(request, keepRows: false);
        return true;
    }

    public void Dispose() => CancelPending();

    private static DiffDocument Present(DiffDocument document) =>
        document.Rows.Count == 0 ? DiffDocument.Message("No changes to show") : document;

    private void Start(WorkingDiffRequest request, bool keepRows)
    {
        CancelPending();
        Request = request;
        IsCut = false;
        Status = DiffStatus.Loading;
        if (!keepRows)
        {
            Document = DiffDocument.Message("Loading...");
        }

        Changed?.Invoke();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        var limited = !_isUnlimited;
        _ = Task.Run(() => LoadAsync(request, limited, token), CancellationToken.None);
    }

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private async Task<DiffReadResult> ReadAsync(WorkingDiffRequest request, int rows, int lineLimit, CancellationToken token)
    {
        var builder = new DiffDocumentBuilder();
        var warnings = new List<string>();
        var truncated = false;
        var fileCut = false;
        var files = reader.ReadWorkingFileAsync(location, request.Side, request.Entry, warnings, lineLimit, Options, token);
        await foreach (var file in files.ConfigureAwait(false))
        {
            builder.Add(file, Options.WholeFile ? Options.ContextLines : null);
            fileCut |= file.IsTruncated;
            if (builder.RowCount >= rows)
            {
                // Leaving the loop disposes the stream, which stops git.
                truncated = true;
                break;
            }
        }

        if (builder.RowCount == 0 && warnings.Count == 0 && NoteForNoFile(request.Entry) is { } note)
        {
            builder.AddTrailingNote(note);
        }

        if (truncated)
        {
            builder.AddTrailingNote($"Diff cut short after {rows} rows (<space>vl loads all of it)");
        }

        return new DiffReadResult(builder.Build(warnings), truncated || fileCut);
    }

    // Git prints no patch for these, and "no changes" would mislead.
    private static string? NoteForNoFile(StatusEntry entry) => entry.Kind switch
    {
        StatusEntryKind.Unmerged => $"Conflicted ({DescribeConflict(entry.Conflict)}): git shows no combined diff for it",
        StatusEntryKind.Untracked => "Nothing to show: an untracked directory or repository, or a file that cannot be read",
        _ => null,
    };

    private static string DescribeConflict(ConflictKind kind) => kind switch
    {
        ConflictKind.BothDeleted => "deleted by both",
        ConflictKind.DeletedByThem => "deleted by them",
        ConflictKind.DeletedByUs => "deleted by us",
        ConflictKind.AddedByUs => "added by us",
        ConflictKind.AddedByThem => "added by them",
        ConflictKind.BothAdded => "added by both",
        _ => "both modified",
    };

    private async Task LoadAsync(WorkingDiffRequest request, bool limited, CancellationToken token)
    {
        try
        {
            var read = await ReadAsync(request, limited ? rowLimit : int.MaxValue, limited ? fileLineLimit : int.MaxValue, token)
                .ConfigureAwait(false);
            postToUi(() => Complete(request, read.Document, string.Empty, read.IsCut, token));
        }
        catch (OperationCanceledException)
        {
            // Another file was asked for (or the tab closed).
        }
        catch (Exception exception)
        {
            postToUi(() => Complete(request, null, exception.Message, false, token));
        }
    }

    private void Complete(WorkingDiffRequest request, DiffDocument? document, string error, bool isCut, CancellationToken token)
    {
        if (token.IsCancellationRequested || Request != request)
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
            if (!isCut)
            {
                _cache.Add(request, document);
            }
        }

        Changed?.Invoke();
    }
}
