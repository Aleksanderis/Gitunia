using Gitunia.App;

namespace Gitunia.Tui.Tests.App;

/// <summary>Posts through a test-supplied delegate, for tests that count, queue or delay the posts.</summary>
public sealed class DelegateUi(Action<Action> post) : IUiInvoker
{
    public void Post(Action action) => post(action);
}
