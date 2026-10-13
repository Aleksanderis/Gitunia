using Gitunia.Core.Diff;
using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>
/// The diff of the commit shown in the diff pane of one tab: the whole commit against its first parent (so merge commits
/// match the file list), read off the UI thread and flattened into a <see cref="DiffDocument"/>. Asking again for the same
/// commit costs nothing; asking for another one cancels the load in flight. UI thread only; results come back through
/// <c>postToUi</c>.
/// </summary>
public sealed class DiffStore(Repo repo, Action<Action> postToUi, int rowLimit = DiffStore.DefaultRowLimit, int fileLineLimit = DiffStore.DefaultFileLineLimit) : IDisposable
{
    /// <summary>Rows kept before the diff is cut short; the pane then says so in a last row.</summary>
    public const int DefaultRowLimit = 400_000;

    /// <summary>Hunk lines kept per file before the file is cut short.</summary>
    public const int DefaultFileLineLimit = 100_000;

    private CancellationTokenSource? pending;
    private ObjectId? firstParent;
    private bool isUnlimited;

    /// <summary>Raised on the UI thread when <see cref="Document"/> or <see cref="Status"/> changed.</summary>
    public event Action? Changed;

    public ObjectId? Commit { get; private set; }

    /// <summary>The loaded diff was cut short by the row or per-file limit, so <see cref="LoadAnyway"/> would show more.</summary>
    public bool IsCut { get; private set; }

    public DiffStatus Status { get; private set; } = DiffStatus.None;

    /// <summary>The rows to show: a remark while loading or after a failure, the diff once loaded.</summary>
    public DiffDocument Document { get; private set; } = DiffDocument.Empty;

    /// <summary>Starts loading the diff of <paramref name="commit"/> unless it is the one shown (or loading) already.</summary>
    /// <param name="commit">The commit.</param>
    /// <param name="firstParent">The first parent to compare against, null for a root commit.</param>
    /// <returns>True when a load was started.</returns>
    public bool Load(ObjectId commit, ObjectId? firstParent)
    {
        if (Commit == commit && Status != DiffStatus.Failed)
        {
            return false;
        }

        isUnlimited = false;
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
        Commit = null;
        firstParent = null;
        IsCut = false;
        Status = DiffStatus.None;
        Document = DiffDocument.Empty;
    }

    public void Dispose() => CancelPending();

    private void CancelPending()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
    }

    private async Task LoadAsync(ObjectId commit, ObjectId? firstParent, bool limited, CancellationToken token)
    {
        try
        {
            var builder = new DiffDocumentBuilder();
            var warnings = new List<string>();
            var truncated = false;
            var fileCut = false;
            var lineLimit = limited ? fileLineLimit : int.MaxValue;
            await foreach (var file in DiffReader.ReadCommitAsync(repo, commit, firstParent, token, warnings, lineLimit).ConfigureAwait(false))
            {
                builder.Add(file);
                fileCut |= file.IsTruncated;
                if (limited && builder.RowCount >= rowLimit)
                {
                    // Leaving the loop disposes the stream, which stops git.
                    truncated = true;
                    break;
                }
            }

            if (truncated)
            {
                builder.AddTrailingNote($"Diff cut short after {rowLimit} rows (L loads all of it)");
            }

            var document = builder.Build(warnings);
            var isCut = truncated || fileCut;
            postToUi(() => Complete(commit, document, string.Empty, isCut, token));
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
            Document = document.Rows.Count == 0 ? DiffDocument.Message("No changes in this commit") : document;
        }

        Changed?.Invoke();
    }
}
