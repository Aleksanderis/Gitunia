using Xunit;

namespace Gitunia.Git.Tests;

public class WatchBudgetTests
{
    [Fact]
    public void GivenCapacity_WhenAcquiringTooMany_ThenNothingIsTaken()
    {
        var budget = new WatchBudget(5);

        Assert.True(budget.TryAcquire(3));
        Assert.False(budget.TryAcquire(3));
        Assert.Equal(3, budget.InUse);
        Assert.True(budget.TryAcquire(2));
    }

    [Fact]
    public void GivenAcquired_WhenReleased_ThenSlotsAreFree()
    {
        var budget = new WatchBudget(2);
        Assert.True(budget.TryAcquire(2));

        budget.Release(2);

        Assert.Equal(0, budget.InUse);
        Assert.True(budget.TryAcquire(2));
    }
}
