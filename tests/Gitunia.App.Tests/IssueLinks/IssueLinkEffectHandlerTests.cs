using Gitunia.App.IssueLinks;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.IssueLinks;
using Gitunia.Platform.Urls;
using Xunit;

namespace Gitunia.App.Tests.IssueLinks;

public class IssueLinkEffectHandlerTests
{
    private const string Url = "https://example.org/1";

    [Fact]
    public async Task GivenAnOpenerThatSucceeds_WhenHandling_ThenOpenedIsPosted()
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new IssueLinkEffectHandler(new FakeOpener(new UrlOpenResult(true)), dispatcher);

        handler.Handle(new OpenUrlEffect(Url));

        var result = await dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new IssueLinkOpenFinished(Url, true, null), result);
    }

    [Fact]
    public async Task GivenAnOpenerThatFails_WhenHandling_ThenTheErrorIsPosted()
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new IssueLinkEffectHandler(new FakeOpener(new UrlOpenResult(false, "no opener")), dispatcher);

        handler.Handle(new OpenUrlEffect(Url));

        var result = await dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new IssueLinkOpenFinished(Url, false, "no opener"), result);
    }

    [Fact]
    public async Task GivenAnOpenerThatThrows_WhenHandling_ThenAFailureIsPosted()
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new IssueLinkEffectHandler(new FakeOpener(null), dispatcher);

        handler.Handle(new OpenUrlEffect(Url));

        var result = await dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new IssueLinkOpenFinished(Url, false, "the opener failed"), result);
    }

    [Fact]
    public async Task GivenASlowOpener_WhenHandling_ThenHandleReturnsBeforeTheOpenerFinishes()
    {
        using var release = new ManualResetEventSlim();
        var dispatcher = new RecordingDispatcher();
        var opener = new FakeOpener(new UrlOpenResult(true)) { Gate = release };
        var handler = new IssueLinkEffectHandler(opener, dispatcher);

        handler.Handle(new OpenUrlEffect(Url));

        Assert.False(opener.Finished);
        release.Set();
        await dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.True(opener.Finished);
    }

    private sealed class FakeOpener(UrlOpenResult? result) : IUrlOpener
    {
        public ManualResetEventSlim? Gate { get; init; }

        public bool Finished { get; private set; }

        public bool IsSupported => true;

        public UrlOpenResult Open(string url)
        {
            Gate?.Wait(TimeSpan.FromSeconds(10));
            Finished = true;

            return result ?? throw new InvalidOperationException("boom");
        }
    }
}
