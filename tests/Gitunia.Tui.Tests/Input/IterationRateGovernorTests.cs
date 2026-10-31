using Gitunia.Tui.Input;
using Gitunia.Tui.Tests.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class IterationRateGovernorTests
{
    private const ushort ActiveRate = 100;
    private const ushort IdleRate = 50;
    private static readonly TimeSpan IdleAfter = TimeSpan.FromSeconds(2);

    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void GivenJustStarted_WhenAsked_ThenTheActiveRateApplies()
    {
        var governor = new IterationRateGovernor(ActiveRate, IdleRate, IdleAfter, _time);

        Assert.Equal(ActiveRate, governor.CurrentRate());
    }

    [Fact]
    public void GivenNoInputForLongerThanTheLimit_WhenAsked_ThenTheIdleRateApplies()
    {
        var governor = new IterationRateGovernor(ActiveRate, IdleRate, IdleAfter, _time);

        _time.Advance(IdleAfter);

        Assert.Equal(IdleRate, governor.CurrentRate());
    }

    [Fact]
    public void GivenNoInputYetButStartedJustBeforeTheLimit_WhenAsked_ThenTheActiveRateStays()
    {
        var governor = new IterationRateGovernor(ActiveRate, IdleRate, IdleAfter, _time);
        _time.Advance(IdleAfter - TimeSpan.FromMilliseconds(1));

        Assert.Equal(ActiveRate, governor.CurrentRate());
    }

    [Fact]
    public void GivenIdle_WhenInputArrives_ThenTheActiveRateIsBackAndIdlesAgainLater()
    {
        var governor = new IterationRateGovernor(ActiveRate, IdleRate, IdleAfter, _time);
        _time.Advance(TimeSpan.FromSeconds(10));

        governor.NoteInput();

        Assert.Equal(ActiveRate, governor.CurrentRate());

        _time.Advance(IdleAfter);

        Assert.Equal(IdleRate, governor.CurrentRate());
    }
}
