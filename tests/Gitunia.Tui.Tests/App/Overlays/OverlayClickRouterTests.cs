using Gitunia.Core.Input;
using Gitunia.Tui.App.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

[Trait("Os", "Independent")]
public class OverlayClickRouterTests
{
    [Fact]
    public void GivenATargetWithoutAnObject_WhenBuildingTheRouter_ThenItFailsAtStartup()
    {
        var targets = new Dictionary<OverlayClickTarget, IOverlayClickTarget>();

        Assert.Throws<InvalidOperationException>(() => new OverlayClickRouter(targets));
    }

    [Fact]
    public void GivenSeveralOverlaysUnderTheClick_WhenClicking_ThenTheFirstInTheRoutingOrderTakesIt()
    {
        var asked = new List<OverlayClickTarget>();
        var order = InputRouting.ClickTargets().ToList();
        var targets = order.ToDictionary(target => target, target => (IOverlayClickTarget)new Target(target, asked, order[1]));
        var router = new OverlayClickRouter(targets);

        var click = router.ClickAt(3, 4);

        Assert.Equal(order[1].ToString(), click?.View);
        Assert.Equal(order.Take(2), asked);
    }

    [Fact]
    public void GivenNoOverlayUnderTheClick_WhenClicking_ThenNothingTakesIt()
    {
        var asked = new List<OverlayClickTarget>();
        var targets = InputRouting.ClickTargets()
            .ToDictionary(target => target, target => (IOverlayClickTarget)new Target(target, asked, null));
        var router = new OverlayClickRouter(targets);

        Assert.Null(router.ClickAt(0, 0));
        Assert.Equal(InputRouting.ClickTargets(), asked);
    }

    private sealed class Target(OverlayClickTarget self, List<OverlayClickTarget> asked, OverlayClickTarget? takes)
        : IOverlayClickTarget
    {
        public OverlayClick? ClickAt(int line, int column)
        {
            asked.Add(self);

            return takes == self ? new OverlayClick(self.ToString()) : null;
        }
    }
}
