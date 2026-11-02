
namespace Gitunia.App.Loading;

/// <summary>The real wait.</summary>
public sealed class TaskDelayer : IDelayer
{
    public static readonly TaskDelayer Instance = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
}
