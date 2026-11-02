using Gitunia.App.Loading;

namespace Gitunia.Tui.Tests.App;

/// <summary>Waits through a test-supplied delegate, for tests that record, block or skip the debounce.</summary>
public sealed class DelegateDelayer(Func<TimeSpan, CancellationToken, Task> delay) : IDelayer
{
    public Task DelayAsync(TimeSpan duration, CancellationToken token) => delay(duration, token);
}
