using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs;

[Trait("Os", "Independent")]
public sealed class TabActivityTests
{
    [Fact]
    public void GivenTwoGates_WhenEitherRunsForATab_ThenTheTabIsBusyUntilItEnds()
    {
        var activity = new TabActivity();
        var branches = activity.CreateGate();
        var pushes = activity.CreateGate();
        var tabId = new TabId(1);

        branches.Begin(tabId);
        var busyWithBranch = activity.IsBusy(tabId);
        branches.End(tabId);
        pushes.Begin(tabId);
        var busyWithPush = activity.IsBusy(tabId);
        pushes.End(tabId);

        Assert.True(busyWithBranch);
        Assert.True(busyWithPush);
        Assert.False(activity.IsBusy(tabId));
    }

    [Fact]
    public void GivenAGateNotFromTheActivity_WhenItRuns_ThenTheActivityDoesNotSeeIt()
    {
        var activity = new TabActivity();
        var tabId = new TabId(1);
        new TabOperationGate().Begin(tabId);

        Assert.False(activity.IsBusy(tabId));
    }

    [Fact]
    public void GivenARunningOperation_WhenBeginningAnother_ThenItIsRefused()
    {
        var gate = new TabActivity().CreateGate();
        var tabId = new TabId(1);

        var first = gate.Begin(tabId);
        var second = gate.Begin(tabId);

        Assert.True(first);
        Assert.False(second);
    }
}
