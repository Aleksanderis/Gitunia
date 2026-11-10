using Gitunia.Core.Integration;
using Gitunia.Core.Text;
using Gitunia.Git.Execution.Exceptions;
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
        EffectWork.Start(
            dispatcher,
            token => PerformAsync(effect, token),
            () => new IntegrationCancelled(effect.TabId),
            exception => WithOutput(Failure(effect, FailureText.Reason(exception)), effect, exception),
            cancellationToken);
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

    // Continue, abort and skip keep the first line in the status bar and give git's whole output to the output box.
    private static IntegrationFailed WithOutput(IntegrationFailed failed, IntegrationEffect effect, Exception exception)
    {
        if (exception is not GitCommandFailedException git)
        {
            return failed;
        }

        var output = effect switch
        {
            AbortOperationEffect abort => OperationFailureOutput.Of(
                "abort", abort.Info, git.StandardError, git.StandardOutput),
            ContinueOperationEffect proceed => OperationFailureOutput.Of(
                "continue", proceed.Info, git.StandardError, git.StandardOutput),
            SkipOperationEffect skip => OperationFailureOutput.Of(
                "skip", skip.Info, git.StandardError, git.StandardOutput),
            _ => null,
        };

        return failed with { Output = output };
    }

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
