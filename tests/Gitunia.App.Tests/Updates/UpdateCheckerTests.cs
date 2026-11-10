using System.Net;
using Gitunia.App.Updates;
using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public sealed class UpdateCheckerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly string _stateDirectory = Path.Combine(Path.GetTempPath(), $"gitunia-update-check-{Guid.NewGuid():N}");
    private readonly FakeReleaseServer _server = new();
    private readonly FakeTime _time = new(Now);

    public void Dispose()
    {
        if (Directory.Exists(_stateDirectory))
        {
            Directory.Delete(_stateDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GivenANewerRelease_WhenChecking_ThenTheNoticeNamesItAndTheCacheIsWritten()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));

        var notice = await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Equal("0.9.0", notice!.Version);
        Assert.Equal("gitunia update", notice.Command);
        Assert.Single(Directory.GetFiles(_stateDirectory));
        Assert.True(File.Exists(Path.Combine(_stateDirectory, UpdateCheckCache.FileName)));
    }

    [Fact]
    public async Task GivenACurrentBuild_WhenChecking_ThenThereIsNoNotice()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.8.0", []));

        Assert.Null(await Checker("0.8.0").CheckAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GivenACheckedToday_WhenCheckingAgain_ThenNoRequestIsMadeAndTheCachedAnswerIsUsed()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));
        await Checker("0.8.0").CheckAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(23));

        var notice = await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Equal("0.9.0", notice!.Version);
        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task GivenACachedAnswerThatTheBuildHasCaughtUpWith_WhenChecking_ThenThereIsNoNotice()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));
        await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Null(await Checker("0.9.0").CheckAsync(CancellationToken.None));
        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task GivenAStaleCache_WhenChecking_ThenANewRequestIsMade()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));
        await Checker("0.8.0").CheckAsync(CancellationToken.None);
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("1.0.0", []));
        _time.Advance(UpdateChecker.Interval);

        var notice = await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Equal("1.0.0", notice!.Version);
        Assert.Equal(2, _server.Requests.Count);
    }

    [Fact]
    public async Task GivenAFailingEndpoint_WhenChecking_ThenThereIsNoNoticeNoCrashAndTheAttemptCounts()
    {
        _server.Fail(FakeRelease.LatestUrl, HttpStatusCode.InternalServerError);

        var first = await Checker("0.8.0").CheckAsync(CancellationToken.None);
        var second = await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task GivenAKnownUpdateAndAFailingRetry_WhenTheDayHasPassed_ThenTheEarlierAnswerStays()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));
        await Checker("0.8.0").CheckAsync(CancellationToken.None);
        _server.Fail(FakeRelease.LatestUrl, HttpStatusCode.InternalServerError);
        _time.Advance(UpdateChecker.Interval);

        var notice = await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Equal("0.9.0", notice!.Version);
    }

    [Fact]
    public async Task GivenADevelopmentBuild_WhenChecking_ThenNothingIsRequestedOrWritten()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));

        var notice = await Checker("0.0.0-dev").CheckAsync(CancellationToken.None);

        Assert.Null(notice);
        Assert.Empty(_server.Requests);
        Assert.False(Directory.Exists(_stateDirectory));
    }

    [Fact]
    public async Task GivenADamagedCacheFile_WhenChecking_ThenItIsReplacedByANewRequest()
    {
        Directory.CreateDirectory(_stateDirectory);
        await File.WriteAllTextAsync(Path.Combine(_stateDirectory, UpdateCheckCache.FileName), "{ not json", TestContext.Current.CancellationToken);
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));

        var notice = await Checker("0.8.0").CheckAsync(CancellationToken.None);

        Assert.Equal("0.9.0", notice!.Version);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenChecking_ThenItIsCancelledAndNothingIsWritten()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Checker("0.8.0").CheckAsync(cancellation.Token));
        Assert.False(File.Exists(Path.Combine(_stateDirectory, UpdateCheckCache.FileName)));
    }

    private UpdateChecker Checker(string version) => new(
        new UpdateCheckCache(_stateDirectory),
        version,
        new Uri(FakeRelease.LatestUrl),
        new InstallFactsReader(Path.Combine(_stateDirectory, "bin", "gitunia")),
        _time,
        _server);

    private sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan span) => _now += span;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
