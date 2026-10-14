using System.Threading.Channels;

namespace Gitunia.Tui.Tests.App;

/// <summary>
/// Stands in for the UI thread in tests: background work posts its callbacks here and the test thread runs them,
/// so state is only touched from one thread and tests wait on posted work instead of sleeping.
/// </summary>
public sealed class UiPump(CancellationToken cancellationToken)
{
    private static readonly TimeSpan GuardTimeout = TimeSpan.FromSeconds(30);

    private readonly Channel<Action> _queue = Channel.CreateUnbounded<Action>();

    public void Post(Action action) => _queue.Writer.TryWrite(action);

    /// <summary>Runs posted callbacks on the calling thread until the condition holds; the guard timeout only bounds a hang.</summary>
    public async Task RunUntilAsync(Func<bool> condition)
    {
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        guard.CancelAfter(GuardTimeout);
        while (!condition())
        {
            var action = await _queue.Reader.ReadAsync(guard.Token);
            action();
        }
    }
}
