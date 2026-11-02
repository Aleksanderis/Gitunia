using Gitunia.Core;
using Gitunia.Core.Rebase;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Commits;
using Gitunia.Git.Rebase;

namespace Gitunia.App.Rebase;

/// <summary>
/// Runs the effects of the interactive rebase editor off the UI thread and posts exactly one result command for each:
/// the todo, a message, what the engine did, a refusal or failure with git's first error line, or cancellation. It
/// never reads or changes state. Timeouts stay in the git layer.
/// </summary>
/// <param name="writer">Reads the range and runs the rebase.</param>
/// <param name="details">Reads the full message of a commit that is reworded.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class RebaseEditorEffectHandler(
    IInteractiveRebaseWriter writer,
    ICommitDetailsReader details,
    IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(RebaseEditorEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(effect, cancellationToken), CancellationToken.None);
    }

    private static RebaseBase BaseOf(string? targetHash) =>
        (targetHash is null ? RebaseBase.Root : RebaseBase.OntoCommit(targetHash)).WithMerges();

    private async Task RunAsync(RebaseEditorEffect effect, CancellationToken token)
    {
        AppCommand result;
        try
        {
            result = await WorkAsync(effect, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = Cancelled(effect);
        }
        catch (RebaseRangeException exception) when (effect is ReadRebaseRangeEffect)
        {
            result = new RebaseEditorReadFailed(effect.TabId, FailureText.Reason(exception), StatusSeverity.Warning);
        }
        catch (Exception exception) when (effect is RunInteractiveRebaseEffect
            && exception is RebaseRangeException or RebaseEnvironmentException)
        {
            // Nothing ran: the todo comes back so the work of editing it is not lost.
            var run = (RunInteractiveRebaseEffect)effect;
            result = new RebaseEditorNotStarted(
                effect.TabId,
                run.State,
                $"Could not start the rebase: {FailureText.Reason(exception)}");
        }
        catch (Exception exception)
        {
            result = Failure(effect, FailureText.Reason(exception));
        }

        dispatcher.Post(result);
    }

    private static AppCommand Cancelled(RebaseEditorEffect effect) => effect is RunInteractiveRebaseEffect
        ? new RebaseCancelled(effect.TabId)
        : new RebaseEditorReadCancelled(effect.TabId);

    private static AppCommand Failure(RebaseEditorEffect effect, string reason) => effect switch
    {
        ReadRebaseRangeEffect => new RebaseEditorReadFailed(
            effect.TabId,
            $"Could not read the commits to rebase: {reason}",
            StatusSeverity.Error),
        RunInteractiveRebaseEffect run => new RebaseFailed(
            effect.TabId,
            $"Could not rebase {run.State.BranchName} onto {run.State.TargetLabel}: {reason}"),
        _ => throw new UnhandledEffectException(effect),
    };

    private Task<AppCommand> WorkAsync(RebaseEditorEffect effect, CancellationToken token) => effect switch
    {
        ReadRebaseMessageEffect message => ReadMessageAsync(message, token),
        ReadRebaseRangeEffect range => ReadRangeAsync(range, token),
        RunInteractiveRebaseEffect run => RunRebaseAsync(run, token),
        _ => throw new UnhandledEffectException(effect),
    };

    private async Task<AppCommand> ReadRangeAsync(ReadRebaseRangeEffect effect, CancellationToken token)
    {
        var rebaseBase = BaseOf(effect.TargetHash);
        var commits = await writer.ReadRangeAsync(effect.Location, rebaseBase, token).ConfigureAwait(false);
        var state = RebaseEditorState.Create(
            effect.TabId,
            effect.BranchName,
            effect.Label,
            rebaseBase,
            effect.OldTip,
            commits);

        return new RebaseEditorRangeRead(effect.TabId, effect.Generation, state, effect.TargetHash);
    }

    // The subject is a safe start when git cannot deliver the whole message.
    private async Task<AppCommand> ReadMessageAsync(ReadRebaseMessageEffect effect, CancellationToken token)
    {
        var text = effect.Subject;
        try
        {
            var read = await details.ReadAsync(effect.Location, effect.Id, null, token).ConfigureAwait(false);
            text = read?.Message ?? text;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Kept: the editor opens with the subject.
        }

        return new RebaseEditorMessageRead(effect.TabId, effect.Generation, effect.Hash, text);
    }

    private async Task<AppCommand> RunRebaseAsync(RunInteractiveRebaseEffect effect, CancellationToken token)
    {
        var state = effect.State;
        var result = await writer.RebaseAsync(effect.Location, state.Base, state.ToEntries(), token)
            .ConfigureAwait(false);

        return new RebaseEditorFinished(effect.TabId, state, result);
    }
}
