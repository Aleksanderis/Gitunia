namespace Gitunia.App.Background;

/// <summary>
/// Where background work (polls, prefetch, the worktree overview, issue links) waits for a slot, so a burst of it never
/// fills the thread pool and process table. User effects do not use it: they start at once.
/// </summary>
public interface IBackgroundWork
{
    /// <summary>Waits for a free slot, runs <paramref name="work"/> and frees the slot. Cancelling while waiting
    /// throws <see cref="OperationCanceledException"/> and the work never starts.</summary>
    Task RunAsync(Func<Task> work, CancellationToken cancellationToken);
}
