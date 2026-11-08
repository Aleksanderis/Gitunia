using Gitunia.Core;

namespace Gitunia.App;

/// <summary>
/// The one run, cancel and post skeleton of the effect handlers: do the work, turn the outcome into exactly one result
/// command and post it. A handler keeps only its work and its failure text. A cancellation counts as the user's only
/// when the token the handler was given requested it; any other <see cref="OperationCanceledException"/> (for example
/// an inner token cancelled while the handler's own was not) is a failure.
/// </summary>
public static class EffectWork
{
    /// <summary>Runs the work off the UI thread and returns at once; the result goes to the dispatcher.</summary>
    /// <param name="dispatcher">Where the result command goes.</param>
    /// <param name="perform">The work; it returns the success command.</param>
    /// <param name="onCancelled">Builds the command for a cancellation requested through the token.</param>
    /// <param name="onFailed">Builds the command for any other exception.</param>
    /// <param name="cancellationToken">The token whose cancellation means the user cancelled.</param>
    /// <param name="tasks">Remembers the work so the shutdown can wait for it; null leaves it untracked.</param>
    public static void Start<TCommand>(
        IDispatcher dispatcher,
        Func<CancellationToken, Task<TCommand>> perform,
        Func<TCommand> onCancelled,
        Func<Exception, TCommand> onFailed,
        CancellationToken cancellationToken,
        ShutdownTasks? tasks = null)
        where TCommand : AppCommand
    {
        Task Work() => RunAsync(perform, onCancelled, onFailed, dispatcher.Post, cancellationToken);
        if (tasks is null)
        {
            _ = Task.Run(Work, CancellationToken.None);
        }
        else
        {
            tasks.Run(Work);
        }
    }

    /// <summary>
    /// Runs the work and hands exactly one result to <paramref name="post"/>. A handler that must do more before the
    /// post (end a linked token source, read the status again) passes its own <paramref name="post"/>; the result of
    /// a cancellation goes to <paramref name="postCancelled"/> when given, else to <paramref name="post"/>.
    /// </summary>
    /// <param name="perform">The work; it returns the success command.</param>
    /// <param name="onCancelled">Builds the command for a cancellation requested through the token.</param>
    /// <param name="onFailed">Builds the command for any other exception.</param>
    /// <param name="post">Receives the success or failure command.</param>
    /// <param name="cancellationToken">The token whose cancellation means the user cancelled.</param>
    /// <param name="postCancelled">Receives the cancel command instead of <paramref name="post"/>.</param>
    public static async Task RunAsync<TCommand>(
        Func<CancellationToken, Task<TCommand>> perform,
        Func<TCommand> onCancelled,
        Func<Exception, TCommand> onFailed,
        Action<TCommand> post,
        CancellationToken cancellationToken,
        Action<TCommand>? postCancelled = null)
    {
        TCommand result;
        try
        {
            result = await perform(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            (postCancelled ?? post)(onCancelled());

            return;
        }
        catch (Exception exception)
        {
            result = onFailed(exception);
        }

        post(result);
    }
}
