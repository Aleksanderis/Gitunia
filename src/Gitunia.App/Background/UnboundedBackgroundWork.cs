namespace Gitunia.App.Background;

/// <summary>A queue with no limit: runs the work at once. For callers and tests that do not share a queue.</summary>
public sealed class UnboundedBackgroundWork : IBackgroundWork
{
    /// <summary>The shared instance; it holds no state.</summary>
    public static UnboundedBackgroundWork Instance { get; } = new();

    private UnboundedBackgroundWork()
    {
    }

    public Task RunAsync(Func<Task> work, CancellationToken cancellationToken) => work();
}
