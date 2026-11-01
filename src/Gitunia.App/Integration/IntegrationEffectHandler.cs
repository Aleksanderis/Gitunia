using Gitunia.Core.Integration;
using Gitunia.Core.Text;
using Gitunia.Git.Repositories;

namespace Gitunia.App.Integration;

/// <summary>
/// Runs the reset, merge and operation effects off the UI thread and posts exactly one result command for each:
/// success with the text for the status bar, failure with git's first error line, or cancellation. It never reads or
/// changes state. Timeouts stay in the git layer.
/// </summary>
/// <param name="dispatcher">Where the result commands go.</param>
/// <param name="reset">Runs the reset effects.</param>
/// <param name="merge">Runs the merge effect.</param>
/// <param name="operations">Runs the continue, abort and skip effects.</param>
public sealed class IntegrationEffectHandler(
    IDispatcher dispatcher,
    ResetWork reset,
    MergeWork merge,
    OperationWork operations)
{
    /// <summary>The handler over the real services.</summary>
    public static IntegrationEffectHandler Create(GitServices services, IDispatcher dispatcher) =>
        new(
            dispatcher,
            new ResetWork(services.Reset, services.Status, new RepositoryOperationDetector()),
            new MergeWork(services.Merge),
            new OperationWork(services.OperationWriter));

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(IntegrationEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(effect, cancellationToken), CancellationToken.None);
    }

    private async Task RunAsync(IntegrationEffect effect, CancellationToken token)
    {
        IntegrationCommand result;
        try
        {
            result = await PerformAsync(effect, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = new IntegrationCancelled(effect.TabId);
        }
        catch (Exception exception)
        {
            result = Failure(effect, FailureText.Reason(exception));
        }

        dispatcher.Post(result);
    }

    private Task<IntegrationCommand> PerformAsync(IntegrationEffect effect, CancellationToken token) => effect switch
    {
        AbortOperationEffect abort => operations.AbortAsync(abort, token),
        ContinueOperationEffect proceed => operations.ContinueAsync(proceed, token),
        MergeBranchEffect mergeBranch => merge.MergeAsync(mergeBranch, token),
        PrepareResetEffect prepare => reset.PrepareAsync(prepare, token),
        ResetBranchEffect resetBranch => reset.ResetAsync(resetBranch, token),
        SkipOperationEffect skip => operations.SkipAsync(skip, token),
        _ => throw new UnhandledEffectException(effect),
    };

    private static IntegrationFailed Failure(IntegrationEffect effect, string reason) => effect switch
    {
        AbortOperationEffect abort => new IntegrationFailed(
            effect.TabId,
            OperationTexts.Failure("abort", abort.Info, reason)),
        ContinueOperationEffect proceed => new IntegrationFailed(
            effect.TabId,
            OperationTexts.Failure("continue", proceed.Info, reason)),
        MergeBranchEffect mergeBranch => new IntegrationFailed(
            effect.TabId,
            MergeTexts.Failure(mergeBranch.Branch, mergeBranch.Target.Label, reason)),
        PrepareResetEffect => new IntegrationFailed(effect.TabId, ResetTexts.StatusFailure(reason), false),
        ResetBranchEffect => new IntegrationFailed(effect.TabId, ResetTexts.Failure(reason)),
        SkipOperationEffect skip => new IntegrationFailed(
            effect.TabId,
            OperationTexts.Failure("skip", skip.Info, reason)),
        _ => throw new UnhandledEffectException(effect),
    };
}
