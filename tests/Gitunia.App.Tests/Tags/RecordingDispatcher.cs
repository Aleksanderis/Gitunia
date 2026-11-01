using System.Threading.Channels;
using Gitunia.Core;

namespace Gitunia.App.Tests.Tags;

/// <summary>Collects the result commands a handler posts, so a test can await the first one.</summary>
public sealed class RecordingDispatcher : IDispatcher
{
    private readonly Channel<AppCommand> _posted = Channel.CreateUnbounded<AppCommand>();

    public void Post(AppCommand command) => _posted.Writer.TryWrite(command);

    /// <summary>Waits for the next posted command; the guard ends a hang.</summary>
    public async Task<AppCommand> NextAsync(CancellationToken cancellationToken)
    {
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        guard.CancelAfter(TimeSpan.FromSeconds(30));

        return await _posted.Reader.ReadAsync(guard.Token);
    }
}
