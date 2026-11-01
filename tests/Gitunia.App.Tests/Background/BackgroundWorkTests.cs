using Gitunia.App.Background;
using Xunit;

namespace Gitunia.App.Tests.Background;

public class BackgroundWorkTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GivenMoreJobsThanSlots_WhenRunning_ThenNoMoreThanTheLimitRunAtOnce()
    {
        using var work = new BackgroundWork(2);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var jobs = Enumerable.Range(0, 5)
            .Select(_ => work.RunAsync(
                () =>
                {
                    Interlocked.Increment(ref started);
                    return release.Task;
                },
                CancellationToken.None))
            .ToArray();

        await WaitUntilAsync(() => work.Running == 2);

        Assert.Equal(2, started);
        Assert.Equal(3, work.Waiting);
        release.SetResult();
        await Task.WhenAll(jobs).WaitAsync(Wait, TestContext.Current.CancellationToken);
        Assert.Equal(5, started);
        Assert.Equal(0, work.Running);
    }

    [Fact]
    public async Task GivenAJobWaitingForASlot_WhenItsTokenIsCancelled_ThenItNeverStarts()
    {
        using var work = new BackgroundWork(1);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = work.RunAsync(() => release.Task, CancellationToken.None);
        await WaitUntilAsync(() => work.Running == 1);
        using var cancellation = new CancellationTokenSource();
        var ran = false;
        var second = work.RunAsync(
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            cancellation.Token);
        await WaitUntilAsync(() => work.Waiting == 1);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        release.SetResult();
        await first.WaitAsync(Wait, TestContext.Current.CancellationToken);
        Assert.False(ran);
        Assert.Equal(0, work.Waiting);
    }

    [Fact]
    public async Task GivenAJobThatThrows_WhenRunning_ThenItsSlotIsFreedForTheNext()
    {
        using var work = new BackgroundWork(1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => work.RunAsync(() => throw new InvalidOperationException("boom"), CancellationToken.None));
        await work.RunAsync(() => Task.CompletedTask, CancellationToken.None)
            .WaitAsync(Wait, TestContext.Current.CancellationToken);

        Assert.Equal(0, work.Running);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the condition");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
