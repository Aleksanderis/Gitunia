using Xunit;

namespace Gitunia.App.Tests;

public class ShutdownTasksTests
{
    [Fact]
    public void GivenATaskThatIgnoresCancellation_WhenWaiting_ThenItReturnsFalseAtTheTimeout()
    {
        var tasks = new ShutdownTasks();
        var release = new TaskCompletionSource();
        tasks.Run(() => release.Task);

        var ended = tasks.Wait(TimeSpan.FromMilliseconds(50));
        release.SetResult();

        Assert.False(ended);
        Assert.True(tasks.Wait(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void GivenAFaultedTask_WhenWaiting_ThenItReturnsTrue()
    {
        var tasks = new ShutdownTasks();
        tasks.Run(() => Task.FromException(new InvalidOperationException("boom")));

        Assert.True(tasks.Wait(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void GivenANegativeTimeout_WhenWaiting_ThenItDoesNotThrow()
    {
        var tasks = new ShutdownTasks();

        Assert.True(tasks.Wait(TimeSpan.FromSeconds(-1)));
    }
}
