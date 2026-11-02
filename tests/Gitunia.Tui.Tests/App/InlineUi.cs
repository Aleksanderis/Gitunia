using Gitunia.App;

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
