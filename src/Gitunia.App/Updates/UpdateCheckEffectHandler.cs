using Gitunia.App.Background;
using Gitunia.Core.Updates;

namespace Gitunia.App.Updates;

/// <summary>
/// Runs the <see cref="CheckForUpdateEffect"/> in the background queue (nobody waits for it) and posts one
/// <see cref="UpdateCheckCompleted"/>; any failure becomes "no hint". A cancelled run (the app is closing) posts nothing.
/// </summary>
/// <param name="checker">The check; null where no state directory is known (tests), which finds nothing.</param>
/// <param name="background">The bounded queue for work nobody waits for.</param>
/// <param name="dispatcher">Where the result goes.</param>
/// <param name="tasks">Remembers the running work so the shutdown can wait for it.</param>
public sealed class UpdateCheckEffectHandler(
    UpdateChecker? checker,
    IBackgroundWork background,
    IDispatcher dispatcher,
    ShutdownTasks tasks)
{
    /// <summary>Starts the check and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="cancellationToken">Ends the request when the app is closing.</param>
    public void Handle(CancellationToken cancellationToken) =>
        tasks.Run(() => RunAsync(cancellationToken));

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        UpdateNotice? notice = null;
        try
        {
            if (checker is not null)
            {
                await background.RunAsync(
                    async () => notice = await checker.CheckAsync(cancellationToken),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            notice = null;
        }

        dispatcher.Post(new UpdateCheckCompleted(notice));
    }
}
