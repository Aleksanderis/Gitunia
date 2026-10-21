using Gitunia.Core.Diff;
using Gitunia.Git;
using Gitunia.Git.Blobs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.Loading.Commits;

namespace Gitunia.Tui.Loading.Trees;

/// <summary>
/// The content of the file opened on the Files tab of one tab, read off the UI thread with <see cref="IBlobReader"/>
/// (which applies the size cap and the binary check). <see cref="Load"/> shows the loading state at once and cancels
/// the read before it (the caller shows <see cref="Current"/> itself, <see cref="Changed"/> comes with the result); a result that arrives after another file was asked for, or after <see cref="Clear"/>, is
/// dropped. UI thread only; results come back through <c>postToUi</c>.
/// </summary>
public sealed class FileContentStore(IBlobReader reader, RepositoryLocation location, Action<Action> postToUi)
    : IDisposable
{
    private CancellationTokenSource? _pending;

    /// <summary>Raised on the UI thread when <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    public FileContentState Current { get; private set; } = FileContentState.Empty;

    /// <summary>Starts reading <paramref name="blob"/>, the file <paramref name="path"/> of <paramref name="commit"/>.</summary>
    /// <param name="size">The size the tree listing gave, so a file over the cap is never read.</param>
    public void Load(ObjectId commit, string path, ObjectId blob, long? size)
    {
        CancelPending();
        Current = new FileContentState(
            commit,
            path,
            CommitDetailsStatus.Loading,
            FileContentDocument.Message(path, "Loading..."));
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        _ = Task.Run(() => LoadAsync(commit, path, blob, size, token), CancellationToken.None);
    }

    /// <summary>Forgets the open file and cancels a read in flight.</summary>
    public void Clear()
    {
        CancelPending();
        Current = FileContentState.Empty;
    }

    public void Dispose() => CancelPending();

    private void CancelPending()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private async Task LoadAsync(ObjectId commit, string path, ObjectId blob, long? size, CancellationToken token)
    {
        try
        {
            var content = await reader.ReadAsync(location, blob, size, token).ConfigureAwait(false);
            var document = FileContentDocument.From(path, content);
            postToUi(() => Complete(commit, path, CommitDetailsStatus.Loaded, document, token));
        }
        catch (OperationCanceledException)
        {
            // Another file was opened, or the pane closed.
        }
        catch (Exception exception)
        {
            var document = FileContentDocument.Message(path, $"Cannot read the file: {exception.Message}");
            postToUi(() => Complete(commit, path, CommitDetailsStatus.Failed, document, token));
        }
    }

    private void Complete(
        ObjectId commit,
        string path,
        CommitDetailsStatus status,
        DiffDocument document,
        CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        Current = new FileContentState(commit, path, status, document);
        Changed?.Invoke();
    }
}
