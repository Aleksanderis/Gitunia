namespace Gitunia.App;

/// <summary>
/// Remembers background tasks that may still run a git process, so the shutdown can wait for them (bounded) after
/// cancelling their tokens. A task that has finished is forgotten.
/// </summary>
public sealed class ShutdownTasks
{
    private readonly Lock _gate = new();
    private readonly HashSet<Task> _running = [];

    /// <summary>Starts <paramref name="work"/> on the thread pool and tracks it until it ends.</summary>
    public void Run(Func<Task> work)
    {
        var task = Task.Run(work, CancellationToken.None);
        lock (_gate)
        {
            _running.Add(task);
        }

        _ = task.ContinueWith(
            finished =>
            {
                lock (_gate)
                {
                    _running.Remove(finished);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Waits until every tracked task has ended or <paramref name="timeout"/> has passed; true when all ended.</summary>
    public bool Wait(TimeSpan timeout)
    {
        Task[] tasks;
        lock (_gate)
        {
            tasks = [.. _running];
        }

        try
        {
            return Task.WaitAll(tasks, timeout);
        }
        catch (AggregateException)
        {
            return true; // The tasks handle their own failures; a faulted one has ended all the same.
        }
    }
}
