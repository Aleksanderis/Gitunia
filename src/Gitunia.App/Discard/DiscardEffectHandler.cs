using Gitunia.App.Staging;
using Gitunia.Core.Discard;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Discard;

namespace Gitunia.App.Discard;

/// <summary>
/// Runs the discard effects off the UI thread and posts exactly one result command for each: success with the text for
/// the status bar, failure with git's first error line, or cancellation. Success and failure are posted only after the
/// status of the tab has been read again (an error may have left the files half changed), so the tab stays busy until
/// the lists the cursor sits in are current. It never reads or changes state. Timeouts stay in the git layer.
/// </summary>
/// <param name="discarder">Throws away whole files and chosen lines.</param>
/// <param name="refresher">Reads the status again.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class DiscardEffectHandler(PatchDiscarder discarder, IStatusRefresher refresher, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(DiscardEffect effect, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => RunAsync(effect, cancellationToken), CancellationToken.None);
    }

    private async Task RunAsync(DiscardEffect effect, CancellationToken token)
    {
        DiscardCommand result;
        try
        {
            result = await PerformAsync(effect, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            dispatcher.Post(new DiscardCancelled(effect.TabId));

            return;
        }
        catch (Exception exception)
        {
            result = new DiscardFailed(effect.TabId, FailureOf(effect, FailureText.Reason(exception)));
        }

        refresher.RefreshThen(effect.TabId, () => dispatcher.Post(result));
    }

    private async Task<DiscardCommand> PerformAsync(DiscardEffect effect, CancellationToken token)
    {
        switch (effect)
        {
            case DiscardFilesEffect files:
                await discarder.DiscardWholeFilesAsync(files.Location, files.Files, token).ConfigureAwait(false);
                var text = DiscardReports.Files(files.Files, files.Skipped);

                return new DiscardSucceeded(effect.TabId, text, StatusSeverity.Info, null);
            case DiscardSelectionEffect selection:
                return await DiscardSelectionAsync(selection, token).ConfigureAwait(false);
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private async Task<DiscardCommand> DiscardSelectionAsync(DiscardSelectionEffect effect, CancellationToken token)
    {
        var picked = effect.Selection;
        var result = await discarder
            .DiscardAsync(effect.Location, picked.Entry, picked.Lines, effect.AllowWholeFile, token)
            .ConfigureAwait(false);
        DiffCursorMove? move = null;
        if (DiscardReports.IsDone(result))
        {
            move = effect.Move;
        }

        return new DiscardSucceeded(
            effect.TabId,
            DiscardReports.Selection(picked, result),
            DiscardReports.SelectionSeverity(result),
            move);
    }

    private static string FailureOf(DiscardEffect effect, string reason) => effect switch
    {
        DiscardFilesEffect => DiscardReports.FilesFailure(reason),
        DiscardSelectionEffect => DiscardReports.SelectionFailure(reason),
        _ => throw new UnhandledEffectException(effect),
    };
}
