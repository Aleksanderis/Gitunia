using Gitunia.Core.Stash;
using Gitunia.Core.Text;
using Gitunia.Git.Stash;

namespace Gitunia.App.Stash;

/// <summary>
/// Runs the stash effects off the UI thread and posts exactly one result command for each: success with the text for
/// the status bar, failure with git's first error line, or cancellation. It never reads or changes state. Timeouts stay
/// in the git layer.
/// </summary>
/// <param name="writer">Runs the stash commands.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class StashEffectHandler(IStashWriter writer, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(StashEffect effect, CancellationToken cancellationToken)
    {
        EffectWork.Start(
            dispatcher,
            token => PerformAsync(effect, token),
            () => new StashOperationCancelled(effect.TabId),
            exception => new StashOperationFailed(
                effect.TabId,
                $"{FailureLead(effect)}: {FailureText.Reason(exception)}"),
            cancellationToken);
    }

    private async Task<StashCommand> PerformAsync(StashEffect effect, CancellationToken token)
    {
        switch (effect)
        {
            case ApplyStashEffect apply:
                var applied = apply.Pop
                    ? await writer.PopAsync(apply.Location, apply.Entry, token).ConfigureAwait(false)
                    : await writer.ApplyAsync(apply.Location, apply.Entry, token).ConfigureAwait(false);
                return Success(effect, StashReports.Apply(applied, apply.Entry, apply.Pop));
            case CheckStashRestoreEffect check:
                var state = await writer.CheckRestoreAsync(check.Location, check.Target.Entry, check.Target.File, token)
                    .ConfigureAwait(false);
                return new StashRestoreChecked(effect.TabId, check.Target, state);
            case DropStashEffect drop:
                var dropped = await writer.DropAsync(drop.Location, drop.Entry, token).ConfigureAwait(false);
                return Success(effect, StashReports.Drop(dropped, drop.Entry));
            case PushStashEffect push:
                var pushed = await writer.PushAsync(push.Location, push.Message, push.IncludeUntracked, token)
                    .ConfigureAwait(false);
                return Success(effect, StashReports.Push(pushed, push.Message, push.IncludeUntracked));
            case RestoreStashFileEffect restore:
                var restored = await writer
                    .RestoreFileAsync(restore.Location, restore.Target.Entry, restore.Target.File, token)
                    .ConfigureAwait(false);
                return Success(effect, StashReports.Restore(restored, restore.Target));
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private static StashOperationSucceeded Success(StashEffect effect, StashReport report) =>
        new(effect.TabId, report.Text, report.Severity);

    private static string FailureLead(StashEffect effect) => effect switch
    {
        ApplyStashEffect apply => $"Could not {(apply.Pop ? "pop" : "apply")} the stash",
        CheckStashRestoreEffect check => $"Could not restore {check.Target.File.Change.Path}",
        DropStashEffect => "Could not drop the stash",
        PushStashEffect => "Could not stash the stash",
        RestoreStashFileEffect restore => $"Could not restore {restore.Target.File.Change.Path}",
        _ => throw new UnhandledEffectException(effect),
    };
}
