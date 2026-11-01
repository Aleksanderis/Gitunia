using Gitunia.Core.Conflicts;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App.Conflicts;

/// <summary>
/// Runs the conflict editor's file work off the UI thread and posts exactly one result command for each: the file
/// read into parts, the result written (never over a file that changed since it was opened), the conflict re-created,
/// a failure with the reason, or the cancellation. It never reads or changes state.
/// </summary>
/// <param name="reader">Reads the conflicted file into parts.</param>
/// <param name="writer">Writes the result into the working tree.</param>
/// <param name="recreator">Re-creates the conflict of a file.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class ConflictEffectHandler(
    IConflictReader reader,
    IConflictWriter writer,
    IConflictRecreator recreator,
    IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="effect">What to do.</param>
    /// <param name="cancellationToken">Ends the work when the tab closes.</param>
    public void Handle(ConflictEffect effect, CancellationToken cancellationToken)
    {
        switch (effect)
        {
            case ReadConflictEffect read:
                Start(effect, token => ReadAsync(read, token), cancellationToken);
                break;
            case RecreateConflictEffect recreate:
                Start(effect, token => RecreateAsync(recreate, token), cancellationToken);
                break;
            case WriteConflictEffect write:
                Start(effect, token => WriteAsync(write, token), cancellationToken);
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static ConflictOperationFailed Failure(ConflictEffect effect, Exception exception)
    {
        var name = PrintableText.Of(effect.Path);
        var reason = FailureText.Reason(exception);
        var text = effect switch
        {
            ReadConflictEffect => $"Could not read {name}: {reason}",
            WriteConflictEffect => $"Could not save {name}: {reason}; your picks are kept",
            _ => $"Could not re-create the conflict of {name}: {reason}",
        };

        return new ConflictOperationFailed(effect.TabId, text, StatusSeverity.Error);
    }

    private void Start(
        ConflictEffect effect,
        Func<CancellationToken, Task<ConflictCommand>> work,
        CancellationToken token) =>
        _ = Task.Run(() => RunAsync(effect, work, token), CancellationToken.None);

    private async Task RunAsync(
        ConflictEffect effect,
        Func<CancellationToken, Task<ConflictCommand>> work,
        CancellationToken token)
    {
        ConflictCommand result;
        try
        {
            result = await work(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = new ConflictOperationCancelled(effect.TabId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result = Failure(effect, exception);
        }

        dispatcher.Post(result);
    }

    private async Task<ConflictCommand> ReadAsync(ReadConflictEffect effect, CancellationToken token)
    {
        var result = await reader.ReadAsync(effect.Location, effect.Path, token).ConfigureAwait(false);

        return new ConflictFileRead(effect.TabId, effect.Generation, effect.Path, effect.Conflict, result);
    }

    // The file is read again first: a file edited since it was opened (an editor, a tool) is never overwritten.
    private async Task<ConflictCommand> WriteAsync(WriteConflictEffect effect, CancellationToken token)
    {
        var fresh = await reader.ReadAsync(effect.Location, effect.Path, token).ConfigureAwait(false);
        if (fresh.Kind != ConflictReadKind.Text || ConflictEditorState.TextOf(fresh.Parts) != effect.OriginalText)
        {
            return new ConflictOperationFailed(effect.TabId, ConflictNotices.FileChanged, StatusSeverity.Warning);
        }

        await writer.WriteAsync(effect.Location, effect.Path, effect.Text, token).ConfigureAwait(false);

        return new ConflictSaved(effect.TabId, effect.Path, effect.Message, effect.IsComplete);
    }

    private async Task<ConflictCommand> RecreateAsync(RecreateConflictEffect effect, CancellationToken token)
    {
        await recreator.RecreateAsync(effect.Location, effect.Path, token).ConfigureAwait(false);
        var fresh = await ReadAfterRecreateAsync(effect.Location, effect.Path, token).ConfigureAwait(false);

        return new ConflictRecreated(effect.TabId, effect.Path, fresh);
    }

    // The file is already rewritten when this runs, so a failed read is reported as such, not as a failed re-create.
    private async Task<ConflictReadResult> ReadAfterRecreateAsync(
        RepositoryLocation location,
        string path,
        CancellationToken token)
    {
        try
        {
            return await reader.ReadAsync(location, path, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConflictReadResult.Unsupported($"it could not be read: {FailureText.Reason(exception)}");
        }
    }
}
