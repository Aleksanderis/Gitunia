using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Text;
using Gitunia.Git;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.History;
using Gitunia.Git.History.Models;

namespace Gitunia.App.FileHistory;

/// <summary>
/// Reads the commits of a file and one commit's diff of it off the UI thread, and posts exactly one result command per
/// read. A newer diff read cancels the one still running, so moving down the list quickly keeps one process. It never
/// reads or changes state. The timeouts stay in the reader layer.
/// </summary>
/// <param name="reader">Reads the commits of a file.</param>
/// <param name="diffs">Reads the diff of a commit.</param>
/// <param name="dispatcher">Where the result commands go.</param>
/// <param name="tasks">Remembers the running work so the shutdown can wait for it; the default is a private set.</param>
public sealed class FileHistoryEffectHandler(IFileHistoryReader reader, IDiffReader diffs, IDispatcher dispatcher, ShutdownTasks? tasks = null)
{
    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();

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
            cancellationToken,
            _tasks);

    /// <summary>Starts the diff read, cancelling the previous one, and returns at once.</summary>
    /// <param name="effect">What to read.</param>
    /// <param name="cancellationToken">Ends the read when the tab closes.</param>
    public void Handle(ReadFileHistoryDiffEffect effect, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancelPrevious(Interlocked.Exchange(ref _diffSource, source));
        _tasks.Run(() => RunDiffAsync(effect, source));
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
        var read = await ReadEntriesAsync(effect, token).ConfigureAwait(false);

        return new FileHistoryLoaded(
            effect.TabId,
            effect.Generation,
            effect.Path,
            effect.Revision,
            read.Items,
            effect.Range);
    }

    private async Task<ParseResult<FileHistoryEntry>> ReadEntriesAsync(
        ReadFileHistoryEffect effect,
        CancellationToken token)
    {
        if (effect.Range is not { } range)
        {
            return await reader
                .ReadAsync(effect.Location, effect.Revision, effect.Path, FileHistoryReducer.EntryLimit, token)
                .ConfigureAwait(false);
        }

        return await reader
            .ReadRangeAsync(
                effect.Location,
                effect.Revision,
                effect.Path,
                range,
                FileHistoryReducer.EntryLimit,
                token)
            .ConfigureAwait(false);
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
        if (effect.Entry.Patch is { } patch)
        {
            // A line history read every patch with the list: nothing more to run.
            var parser = new DiffParser(MaximumDiffLines);
            var files = new List<DiffFile>();
            parser.Feed(Encoding.UTF8.GetBytes(patch), files);
            parser.Complete(files);
            warnings.AddRange(parser.Warnings);
            foreach (var file in files)
            {
                builder.Add(file);
            }
        }
        else
        {
            var command = FileHistoryDiffCommand.For(effect.Entry);
            await foreach (var file in diffs
                .ReadAsync(effect.Location, command, warnings, MaximumDiffLines, token)
                .ConfigureAwait(false))
            {
                builder.Add(file);
            }
        }

        var document = builder.RowCount == 0 ? DiffDocument.Message(NoChangesText) : builder.Build(warnings);

        return new FileHistoryDiffLoaded(effect.Generation, document);
    }
}
