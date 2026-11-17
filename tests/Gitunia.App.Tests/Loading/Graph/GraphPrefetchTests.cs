using Gitunia.App.Loading.Graph;
using Xunit;

namespace Gitunia.App.Tests.Loading.Graph;

public sealed class GraphPrefetchTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly FakeCommitLogReader _commitLog = FakeCommitLogReader.Chain(2);
    private readonly GraphPrefetch _prefetch;

    public GraphPrefetchTests()
    {
        _prefetch = new GraphPrefetch(new GraphLoader(_commitLog));
    }

    [Fact]
    public async Task GivenConfirmedLocation_WhenClaimingTwice_ThenOnlyTheFirstGetsTheRead()
    {
        var location = FakeStatusReader.LocationOf("claim");
        _prefetch.Start(Path.GetTempPath());
        _prefetch.Confirm(location);

        using var first = _prefetch.Claim(location);
        using var second = _prefetch.Claim(location);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.True(await first.First.WaitAsync(Patience, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GivenNoConfirmation_WhenClaiming_ThenNothingIsHanded()
    {
        _prefetch.Start(Path.GetTempPath());

        Assert.Null(_prefetch.Claim(FakeStatusReader.LocationOf("unconfirmed")));
    }

    [Fact]
    public void GivenAnotherLocationConfirmed_WhenClaimingTheOther_ThenNothingIsHanded()
    {
        _prefetch.Start(Path.GetTempPath());
        _prefetch.Confirm(FakeStatusReader.LocationOf("one"));

        Assert.Null(_prefetch.Claim(FakeStatusReader.LocationOf("two")));
    }

    [Fact]
    public void GivenMissingDirectory_WhenStarting_ThenThereIsNothingToClaim()
    {
        var location = FakeStatusReader.LocationOf("missing");

        _prefetch.Start(Path.Combine(Path.GetTempPath(), "gitunia-missing-1392"));
        _prefetch.Confirm(location);

        Assert.Null(_prefetch.Claim(location));
        Assert.Equal(0, _commitLog.ReadCount);
    }

    [Fact]
    public void GivenRunningRead_WhenDiscarding_ThenGitIsCancelledAndNothingIsClaimable()
    {
        var location = FakeStatusReader.LocationOf("discard");
        _prefetch.Start(Path.GetTempPath());
        Assert.True(SpinWait.SpinUntil(() => _commitLog.ReadCount == 1, Patience));
        _prefetch.Confirm(location);

        _prefetch.Discard();

        Assert.True(SpinWait.SpinUntil(() => _commitLog.LastTipToken.IsCancellationRequested, Patience));
        Assert.Null(_prefetch.Claim(location));
    }

    [Fact]
    public void GivenUnclaimedRead_WhenItExpires_ThenItIsCancelledAndNothingIsClaimable()
    {
        // Long enough for the tip read to start before the expiry on a cold, slow runner (50 ms lost that race on CI).
        using var expiring =new GraphPrefetch(new GraphLoader(_commitLog), TimeSpan.FromMilliseconds(500));
        var location = FakeStatusReader.LocationOf("expire");
        expiring.Start(Path.GetTempPath());
        expiring.Confirm(location);

        Assert.True(SpinWait.SpinUntil(() => _commitLog.LastTipToken.IsCancellationRequested, Patience));
        Assert.Null(expiring.Claim(location));
    }

    [Fact]
    public async Task GivenClaimedRead_WhenTheOwnerCancels_ThenTheReadStops()
    {
        var location = FakeStatusReader.LocationOf("cancel");
        _prefetch.Start(Path.GetTempPath());
        _prefetch.Confirm(location);
        using var claimed = _prefetch.Claim(location);
        Assert.NotNull(claimed);
        await claimed.First.WaitAsync(Patience, TestContext.Current.CancellationToken);

        claimed.Cancel();

        Assert.True(claimed.IsCancelled);
        Assert.True(_commitLog.LastTipToken.IsCancellationRequested);
        await claimed.Batches.DisposeAsync();
    }

    public void Dispose() => _prefetch.Dispose();
}
