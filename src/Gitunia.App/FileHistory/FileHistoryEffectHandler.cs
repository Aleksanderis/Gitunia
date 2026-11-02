using Gitunia.Core.Diff;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.History;

namespace Gitunia.App.FileHistory;

/// <summary>
/// Reads the commits of a file and one commit's diff of it off the UI thread, and posts exactly one result command per
/// read. A newer diff read cancels the one still running, so moving down the list quickly keeps one process. It never
/// reads or changes state. The timeouts stay in the reader layer.
/// </summary>
/// <param name="reader">Reads the commits of a file.</param>
/// <param name="diffs">Reads the diff of a commit.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class FileHistoryEffectHandler(IFileHistoryReader reader, IDiffReader diffs, IDispatcher dispatcher)
{
    /// <summary>Diff lines kept of the file; the rest are dropped.</summary>
    public const int MaximumDiffLines = 5000;

    /// <summary>The row shown when the commit has nothing to show for the file.</summary>
    public const string NoChangesText = "No changes to this file in this commit";

    private CancellationTokenSource? _diffSource;

    /// <summary>Starts the read and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="effect">What to read.</param>
    /// <param name="cancellationToken">Ends the read when the tab closes.</param>
    public void Handle(ReadFileHistoryEffect effect, CancellationToken cancellationToken) =>
        EffectWork.Start(
            dispatcher,
            token => ReadAsync(effect, token),
            () => new FileHistoryReadCancelled(),
            exception => new FileHistoryReadFailed(
                $"Could not read the history of {effect.Path}: {FailureText.Reason(exception)}"),
            cancellationToken);

    /// <summary>Starts the diff read, cancelling the previous one, and returns at once.</summary>
    /// <param name="effect">What to read.</param>
    /// <param name="cancellationToken">Ends the read when the tab closes.</param>
    public void Handle(ReadFileHistoryDiffEffect effect, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancelPrevious(Interlocked.Exchange(ref _diffSource, source));
        _ = Task.Run(() => RunDiffAsync(effect, source), CancellationToken.None);
    }

    private static void CancelPrevious(CancellationTokenSource? previous)
    {
        try
        {
            previous?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The earlier read had already finished and released its source.
        }
    }

    private async Task<FileHistoryCommand> ReadAsync(ReadFileHistoryEffect effect, CancellationToken token)
    {
        var read = await reader
            .ReadAsync(effect.Location, effect.Revision, effect.Path, FileHistoryReducer.EntryLimit, token)
            .ConfigureAwait(false);

        return new FileHistoryLoaded(effect.TabId, effect.Generation, effect.Path, effect.Revision, read.Items);
    }

    private async Task RunDiffAsync(ReadFileHistoryDiffEffect effect, CancellationTokenSource source)
    {
        await EffectWork.RunAsync(
            token => ReadDiffAsync(effect, token),
            () => new FileHistoryDiffCancelled(),
            exception => new FileHistoryDiffFailed(
                effect.Generation,
                $"Could not read the diff of {effect.Entry.Path}: {FailureText.Reason(exception)}"),
            dispatcher.Post,
            source.Token).ConfigureAwait(false);
        Interlocked.CompareExchange(ref _diffSource, null, source);
        source.Dispose();
    }

    private async Task<FileHistoryCommand> ReadDiffAsync(ReadFileHistoryDiffEffect effect, CancellationToken token)
    {
        var warnings = new List<string>();
        var builder = new DiffDocumentBuilder();
        var command = FileHistoryDiffCommand.For(effect.Entry);
        await foreach (var file in diffs
            .ReadAsync(effect.Location, command, warnings, MaximumDiffLines, token)
            .ConfigureAwait(false))
        {
            builder.Add(file);
        }

        var document = builder.RowCount == 0 ? DiffDocument.Message(NoChangesText) : builder.Build(warnings);

        return new FileHistoryDiffLoaded(effect.Generation, document);
    }
}
