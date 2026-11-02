using Xunit;

namespace Gitunia.App.Tests;

public class EffectWorkTests
{
    private const string Succeeded = "succeeded";
    private const string Cancelled = "cancelled";
    private const string Failed = "failed";

    [Fact]
    public async Task GivenWorkThatSucceeds_WhenRunning_ThenOnlyTheSuccessIsPosted()
    {
        var posted = new List<string>();

        await EffectWork.RunAsync(
            _ => Task.FromResult(Succeeded),
            () => Cancelled,
            _ => Failed,
            posted.Add,
            CancellationToken.None);

        Assert.Equal([Succeeded], posted);
    }

    [Fact]
    public async Task GivenOurTokenCancelled_WhenTheWorkThrowsCancellation_ThenTheCancelResultIsPosted()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var posted = new List<string>();

        await EffectWork.RunAsync<string>(
            Task.FromCanceled<string>,
            () => Cancelled,
            _ => Failed,
            posted.Add,
            source.Token);

        Assert.Equal([Cancelled], posted);
    }

    [Fact]
    public async Task GivenACancellationThatIsNotOurs_WhenTheWorkThrowsIt_ThenItIsAFailure()
    {
        var posted = new List<string>();

        await EffectWork.RunAsync<string>(
            _ => throw new OperationCanceledException("a git timeout"),
            () => Cancelled,
            _ => Failed,
            posted.Add,
            CancellationToken.None);

        Assert.Equal([Failed], posted);
    }

    [Fact]
    public async Task GivenWorkThatThrows_WhenRunning_ThenTheFailureIsBuiltFromTheException()
    {
        var posted = new List<string>();

        await EffectWork.RunAsync<string>(
            _ => throw new InvalidOperationException("boom"),
            () => Cancelled,
            exception => exception.Message,
            posted.Add,
            CancellationToken.None);

        Assert.Equal(["boom"], posted);
    }

    [Fact]
    public async Task GivenASeparateCancelTarget_WhenCancelled_ThenOnlyThatTargetReceivesTheCancelResult()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var posted = new List<string>();
        var postedCancelled = new List<string>();

        await EffectWork.RunAsync<string>(
            Task.FromCanceled<string>,
            () => Cancelled,
            _ => Failed,
            posted.Add,
            source.Token,
            postedCancelled.Add);

        Assert.Empty(posted);
        Assert.Equal([Cancelled], postedCancelled);
    }
}
