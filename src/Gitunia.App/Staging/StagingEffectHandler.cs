using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Staging;

namespace Gitunia.App.Staging;

/// <summary>
/// Runs the staging effects off the UI thread and posts exactly one result command for each: success with the text for
/// the status bar, failure with git's first error line, or cancellation. Success and failure are posted only after the
/// status of the tab has been read again (an error may have left the index half changed), so the tab stays busy until
/// the lists the cursor sits in are current. It never reads or changes state. Timeouts stay in the git layer.
/// </summary>
/// <param name="writer">Stages and unstages whole files.</param>
/// <param name="patchStager">Stages and unstages the hunk or lines picked in a diff.</param>
/// <param name="refresher">Reads the status again.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class StagingEffectHandler(
    IIndexWriter writer,
    PatchStager patchStager,
    IStatusRefresher refresher,
    IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(StagingEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(effect, cancellationToken), CancellationToken.None);
    }

    // Success and failure wait for the status refresh; a cancellation is posted at once.
    private Task RunAsync(StagingEffect effect, CancellationToken token) =>
        EffectWork.RunAsync(
            innerToken => PerformAsync(effect, innerToken),
            () => new StagingCancelled(effect.TabId),
            exception => new StagingFailed(effect.TabId, FailureOf(effect, FailureText.Reason(exception))),
            result => refresher.RefreshThen(effect.TabId, () => dispatcher.Post(result)),
            token,
            dispatcher.Post);

    private async Task<StagingCommand> PerformAsync(StagingEffect effect, CancellationToken token)
    {
        switch (effect)
        {
            case StagePathsEffect paths:
                if (paths.IsStage)
                {
                    await writer.StageAsync(paths.Location, paths.Plan.Paths, token).ConfigureAwait(false);
                }
                else
                {
                    await writer.UnstageAsync(paths.Location, paths.Plan.Paths, paths.IsUnborn, token)
                        .ConfigureAwait(false);
                }

                var text = StagingReports.Paths(paths.Plan, paths.IsStage);

                var detail = paths.Plan.IsResolve
                    ? UndoNotes.Undo($"git checkout --merge -- {string.Join(' ', paths.Plan.Paths)} (overwrites the file)")
                    : null;

                return new StagingSucceeded(effect.TabId, text, StatusSeverity.Info, paths.Follow, detail);
            case StageSelectionEffect selection:
                return await StageSelectionAsync(selection, token).ConfigureAwait(false);
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private async Task<StagingCommand> StageSelectionAsync(StageSelectionEffect effect, CancellationToken token)
    {
        var picked = effect.Selection;

        // Lines picked one by one are never turned into a whole-file stage behind the user's back.
        var result = await patchStager
            .StageAsync(
                effect.Location,
                picked.Side,
                picked.Entry,
                picked.Lines,
                !picked.IsRange,
                effect.IsUnborn,
                token)
            .ConfigureAwait(false);
        var follow = StagingFollow.None;
        if (StagingReports.IsDone(result))
        {
            follow = new StagingFollow(Move: effect.Move);
        }

        return new StagingSucceeded(
            effect.TabId,
            StagingReports.Selection(picked, result),
            StagingReports.SelectionSeverity(result),
            follow);
    }

    private static string FailureOf(StagingEffect effect, string reason) => effect switch
    {
        StagePathsEffect paths => StagingReports.PathsFailure(paths.IsStage, reason),
        StageSelectionEffect selection => StagingReports.SelectionFailure(selection.Selection, reason),
        _ => throw new UnhandledEffectException(effect),
    };
}
