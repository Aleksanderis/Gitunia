using Gitunia.App;
using Gitunia.App.Loading;

namespace Gitunia.Tui.Tests.App;

/// <summary>Stands in for the UI thread in loader tests: runs posted actions immediately, one at a time.</summary>
public sealed class InlineUi : IUiInvoker
{
    public static readonly InlineUi Instance = new();

    private readonly Lock _gate = new();

    public void Post(Action action)
    {
        lock (_gate)
        {
            action();
        }
    }
}

/// <summary>Posts through a test-supplied delegate, for tests that count, queue or delay the posts.</summary>
public sealed class DelegateUi(Action<Action> post) : IUiInvoker
{
    public void Post(Action action) => post(action);
}

/// <summary>Waits through a test-supplied delegate, for tests that record, block or skip the debounce.</summary>
public sealed class DelegateDelayer(Func<TimeSpan, CancellationToken, Task> delay) : IDelayer
{
    public Task DelayAsync(TimeSpan duration, CancellationToken token) => delay(duration, token);
}
