using Gitunia.Core.Ignore;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Ignore;
using Gitunia.App.Staging;

namespace Gitunia.App.Ignore;

/// <summary>
/// Runs the ignore effects off the UI thread and posts exactly one result command for each: success with the text for
/// the status bar, failure with the first error line, or cancellation. Success and failure are posted only after the
/// status of the tab has been read again, so the file is already gone from the Unstaged list when the tab is free. It
/// never reads or changes state. Timeouts stay in the writer.
/// </summary>
/// <param name="writer">Appends the pattern to <c>.gitignore</c>.</param>
/// <param name="refresher">Reads the status again.</param>
/// <param name="dispatcher">Where the result commands go.</param>
/// <param name="tasks">Remembers the running work so the shutdown can wait for it; the default is a private set.</param>
public sealed class IgnoreEffectHandler(
    IIgnoreWriter writer,
    IStatusRefresher refresher,
    IDispatcher dispatcher,
    ShutdownTasks? tasks = null)
{
    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();

    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(IgnoreEffect effect, CancellationToken cancellationToken)
    {
        _tasks.Run(() => RunAsync(effect, cancellationToken));
    }

    // Success and failure wait for the status refresh; a cancellation is posted at once.
    private Task RunAsync(IgnoreEffect effect, CancellationToken token) =>
        EffectWork.RunAsync(
            innerToken => PerformAsync(effect, innerToken),
            () => new IgnoreCancelled(effect.TabId),
            exception => new IgnoreFailed(effect.TabId, IgnoreReports.Failure(FailureText.Reason(exception))),
            result => refresher.RefreshThen(effect.TabId, () => dispatcher.Post(result)),
            token,
            dispatcher.Post);

    private async Task<IgnoreCommand> PerformAsync(IgnoreEffect effect, CancellationToken token)
    {
        if (effect is not AddIgnorePatternEffect add)
        {
            throw new UnhandledEffectException(effect);
        }

        var outcome = await writer.AddAsync(add.Location, add.Pattern, token).ConfigureAwait(false);

        return new IgnoreSucceeded(effect.TabId, IgnoreReports.Done(add.Pattern, outcome), SeverityOf(outcome));
    }

    private static StatusSeverity SeverityOf(IgnoreAddOutcome outcome) =>
        outcome == IgnoreAddOutcome.Added ? StatusSeverity.Info : StatusSeverity.Warning;
}
