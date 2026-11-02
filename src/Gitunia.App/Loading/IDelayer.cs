
namespace Gitunia.App.Loading;

/// <summary>Waits out a debounce. The default is <see cref="Task.Delay(TimeSpan, CancellationToken)"/>; tests inject one they control.</summary>
public interface IDelayer
{
    /// <summary>Completes after <paramref name="duration"/>, or throws when <paramref name="token"/> is cancelled first.</summary>
    Task DelayAsync(TimeSpan duration, CancellationToken token);
}
