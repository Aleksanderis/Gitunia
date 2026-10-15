using Gitunia.Tui.App.ExternalTools;
using Xunit;

namespace Gitunia.Tui.Tests.App.ExternalTools;

public class SuspendableUiInvokerTests
{
    private readonly List<string> _ran = [];

    [Fact]
    public void GivenARunningUi_WhenInvoking_ThenTheActionGoesToTheTarget()
    {
        var invoker = new SuspendableUiInvoker(action => action());

        invoker.Invoke(() => _ran.Add("a"));

        Assert.Equal(["a"], _ran);
    }

    [Fact]
    public void GivenASuspendedUi_WhenInvoking_ThenNothingRunsAndNothingThrowsUntilResumed()
    {
        var invoker = new SuspendableUiInvoker(_ => throw new InvalidOperationException("disposed application"));
        invoker.Suspend();

        invoker.Invoke(() => _ran.Add("a"));
        invoker.Invoke(() => _ran.Add("b"));

        Assert.True(invoker.IsSuspended);
        Assert.Empty(_ran);

        invoker.Resume(action => action());

        Assert.False(invoker.IsSuspended);
        Assert.Equal(["a", "b"], _ran);
    }

    [Fact]
    public void GivenAResumedUi_WhenInvoking_ThenTheNewTargetIsUsed()
    {
        var invoker = new SuspendableUiInvoker(_ => throw new InvalidOperationException("old application"));
        invoker.Suspend();
        invoker.Resume(action => action());

        invoker.Invoke(() => _ran.Add("c"));

        Assert.Equal(["c"], _ran);
    }
}
