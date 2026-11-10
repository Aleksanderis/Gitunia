using Gitunia.App.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public sealed class ReleaseClientTests : IDisposable
{
    private readonly FakeReleaseServer _server = new();
    private readonly HttpClient _client;
    private readonly ReleaseClient _releaseClient;

    public ReleaseClientTests()
    {
        _client = new HttpClient(_server);
        _releaseClient = new ReleaseClient(_client, new Uri(FakeRelease.LatestUrl));
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task GivenAReleaseDocument_WhenReadingTheLatest_ThenTheVersionAndAssetsAreRead()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("2.0.1", ["a.zip", "a.zip.sha256"]));

        var release = await _releaseClient.GetLatestAsync(CancellationToken.None);

        Assert.Equal("2.0.1", release.Version.ToString());
        Assert.Equal(["a.zip", "a.zip.sha256"], release.Assets.Select(asset => asset.Name));
        Assert.Equal($"{FakeRelease.BaseUrl}a.zip", release.FindBySuffix(".zip")!.Url.ToString());
    }

    [Theory]
    [InlineData("{\"tag_name\":\"nightly\",\"assets\":[]}")]
    [InlineData("{\"assets\":[]}")]
    [InlineData("not json")]
    public async Task GivenADocumentWithoutAUsableTag_WhenReadingTheLatest_ThenItFailsWithAMessage(string body)
    {
        _server.Serve(FakeRelease.LatestUrl, body);

        await Assert.ThrowsAsync<UpdateException>(() => _releaseClient.GetLatestAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReadingTheLatest_ThenTheCancellationIsNotWrapped()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("2.0.1", []));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _releaseClient.GetLatestAsync(cancellation.Token));
    }

    [Fact]
    public void GivenAVersion_WhenCreatingTheClient_ThenTheUserAgentNamesGitunia()
    {
        using var client = ReleaseClient.Create("1.2.3");

        Assert.Equal("gitunia/1.2.3", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }
}
