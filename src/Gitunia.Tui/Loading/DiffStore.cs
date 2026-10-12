using Gitunia.Core.Diff;
using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>
/// The diff of the commit shown in the diff pane of one tab: the whole commit against its first parent (so merge commits
/// match the file list), read off the UI thread and flattened into a <see cref="DiffDocument"/>. Asking again for the same
/// commit costs nothing; asking for another one cancels the load in flight. UI thread only; results come back through
/// <c>postToUi</c>.
/// </summary>
public sealed class DiffStore(Repo repo, Action<Action> postToUi, int rowLimit = DiffStore.DefaultRowLimit) : IDisposable
{
    /// <summary>Rows kept before the diff is cut short; the pane then says so in a last row.</summary>
    public const int DefaultRowLimit = 400_000;

    private CancellationTokenSource? pending;

    /// <summary>Raised on the UI thread when <see cref="Document"/> or <see cref="Status"/> changed.</summary>
    public event Action? Changed;

    public ObjectId? Commit { get; private set; }

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

        CancelPending();
        Commit = commit;
        Status = DiffStatus.Loading;
        Document = DiffDocument.Message("Loading...");
        Changed?.Invoke();
        pending = new CancellationTokenSource();
        var token = pending.Token;
        _ = Task.Run(() => LoadAsync(commit, firstParent, token), CancellationToken.None);
        return true;
    }

    /// <summary>Forgets the diff (the pane was closed), cancelling a load in flight.</summary>
    public void Clear()
    {
        CancelPending();
        Commit = null;
        Status = DiffStatus.None;
        Document = DiffDocument.Empty;
    }

    public void Dispose() => CancelPending();

    private static string[] CommandFor(ObjectId commit, ObjectId? firstParent) => firstParent is { } parent
        ? ["diff", parent.ToString(), commit.ToString()]
        : ["show", "--format=", commit.ToString()];

    private void CancelPending()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
    }

    private async Task LoadAsync(ObjectId commit, ObjectId? firstParent, CancellationToken token)
    {
        try
        {
            var builder = new DiffDocumentBuilder();
            var warnings = new List<string>();
            var truncated = false;
            await foreach (var file in DiffReader.ReadAsync(repo, CommandFor(commit, firstParent), token, warnings).ConfigureAwait(false))
            {
                builder.Add(file);
                if (builder.RowCount >= rowLimit)
                {
                    // Leaving the loop disposes the stream, which stops git.
                    truncated = true;
                    break;
                }
            }

            if (truncated)
            {
                builder.AddTrailingNote($"Diff cut short after {rowLimit} rows");
            }

            var document = builder.Build(warnings);
            postToUi(() => Complete(commit, document, string.Empty, token));
        }
        catch (OperationCanceledException)
        {
            // Another commit was asked for (or the tab closed).
        }
        catch (Exception exception)
        {
            postToUi(() => Complete(commit, null, exception.Message, token));
        }
    }

    private void Complete(ObjectId commit, DiffDocument? document, string error, CancellationToken token)
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
            Document = document.Rows.Count == 0 ? DiffDocument.Message("No changes in this commit") : document;
        }

        Changed?.Invoke();
    }
}
