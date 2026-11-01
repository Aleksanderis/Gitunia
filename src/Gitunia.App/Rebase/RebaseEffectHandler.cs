using Gitunia.Core.Rebase;
using Gitunia.Core.Text;
using Gitunia.Git.Rebase;

namespace Gitunia.App.Rebase;

/// <summary>
/// Runs the rebase effects off the UI thread and posts exactly one result command for each: the merge count, the plan,
/// what git did, a failure with git's first error line, or cancellation. It never reads or changes state. Timeouts stay
/// in the git layer.
/// </summary>
/// <param name="rebase">Counts the merges and runs the rebase onto a commit.</param>
/// <param name="quick">Plans and runs fixup, reword and drop.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class RebaseEffectHandler(IRebaseWriter rebase, IQuickRebaseWriter quick, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(RebaseEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(effect, cancellationToken), CancellationToken.None);
    }

    private static RebaseCommand Failure(RebaseEffect effect, string reason) => effect switch
    {
        PlanQuickRebaseEffect plan => new QuickRebasePlanFailed(
            effect.TabId,
            QuickRebaseTexts.PlanFailure(plan.QuickKind, plan.CommitHash, reason)),
        RunQuickRebaseEffect run => new RebaseFailed(effect.TabId, QuickRebaseTexts.Failure(run.Target.Plan, reason)),
        RunRebaseEffect run => new RebaseFailed(effect.TabId, RebaseTexts.Failure(run.Target, reason)),
        // The count only decides on a warning; the rebase itself reports git's reason if the range is bad.
        CountRebaseMergesEffect count => new RebaseMergesCounted(effect.TabId, count.Target, 0),
        _ => throw new UnhandledEffectException(effect),
    };

    private async Task RunAsync(RebaseEffect effect, CancellationToken token)
    {
        RebaseCommand result;
        try
        {
            result = await WorkAsync(effect, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new RebaseCancelled(effect.TabId);
        }
        catch (Exception exception)
        {
            result = Failure(effect, FailureText.Reason(exception));
        }

        dispatcher.Post(result);
    }

    private Task<RebaseCommand> WorkAsync(RebaseEffect effect, CancellationToken token) => effect switch
    {
        CountRebaseMergesEffect count => CountAsync(count, token),
        PlanQuickRebaseEffect plan => PlanAsync(plan, token),
        RunQuickRebaseEffect run => RunQuickAsync(run, token),
        RunRebaseEffect run => RunRebaseAsync(run, token),
        _ => throw new UnhandledEffectException(effect),
    };

    private async Task<RebaseCommand> CountAsync(CountRebaseMergesEffect effect, CancellationToken token)
    {
        var merges = await rebase.CountMergesAsync(effect.Location, effect.Target.CommitHash, token)
            .ConfigureAwait(false);

        return new RebaseMergesCounted(effect.TabId, effect.Target, merges);
    }

    private async Task<RebaseCommand> PlanAsync(PlanQuickRebaseEffect effect, CancellationToken token)
    {
        var plan = await quick.PlanAsync(effect.Location, effect.QuickKind, effect.CommitHash, token)
            .ConfigureAwait(false);

        return new QuickRebasePlanned(effect.TabId, plan);
    }

    private async Task<RebaseCommand> RunRebaseAsync(RunRebaseEffect effect, CancellationToken token)
    {
        var result = await rebase.RebaseAsync(effect.Location, effect.Target.CommitHash, token).ConfigureAwait(false);

        return new RebaseFinished(effect.TabId, effect.Target, result);
    }

    private async Task<RebaseCommand> RunQuickAsync(RunQuickRebaseEffect effect, CancellationToken token)
    {
        var target = effect.Target;
        var result = await quick.RunAsync(effect.Location, target.Plan, target.NewSubject, token).ConfigureAwait(false);

        return new QuickRebaseFinished(effect.TabId, target, result);
    }
}
