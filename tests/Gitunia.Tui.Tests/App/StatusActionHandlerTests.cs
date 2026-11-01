using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class StatusActionHandlerTests : IDisposable
{
    private TabSession? _session;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void GivenAMessage_WhenDismissing_ThenItIsHidden()
    {
        var statuses = new StatusChannel();
        statuses.Error("boom");
        var handler = new StatusActionHandler(statuses, () => null);

        Assert.True(handler.TryHandle(ActionIds.StatusDismiss));

        Assert.Null(statuses.Log.Latest);
        Assert.Single(statuses.Log.History);
        Assert.Equal(string.Empty, handler.Message);
    }

    [Fact]
    public void GivenAMessage_WhenEscapeFallsThrough_ThenItIsDismissed()
    {
        var statuses = new StatusChannel();
        statuses.Warning("careful");
        var handler = new StatusActionHandler(statuses, () => null);

        Assert.True(handler.TryHandleFallback(ActionIds.Cancel));

        Assert.Null(statuses.Log.Latest);
    }

    [Fact]
    public void GivenAnExpiredMessage_WhenEscapeFallsThrough_ThenNothingIsDismissedOrRedrawn()
    {
        var clock = new ManualTimeProvider();
        var statuses = new StatusChannel(clock);
        statuses.Post("done");
        clock.Advance(StatusLifetime.Of(StatusSeverity.Info));
        var changes = 0;
        statuses.Changed += () => changes++;
        var handler = new StatusActionHandler(statuses, () => null);

        handler.TryHandleFallback(ActionIds.Cancel);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenAShowingMessage_WhenEscapeFallsThrough_ThenItIsDismissedBeforeItExpires()
    {
        var clock = new ManualTimeProvider();
        var statuses = new StatusChannel(clock);
        statuses.Error("boom");
        clock.Advance(TimeSpan.FromSeconds(10));
        var handler = new StatusActionHandler(statuses, () => null);

        handler.TryHandleFallback(ActionIds.Cancel);

        Assert.Null(statuses.Current);
    }

    [Fact]
    public void GivenEscape_WhenHandlingBeforeThePanes_ThenItIsNotClaimed()
    {
        var statuses = new StatusChannel();
        statuses.Warning("careful");
        var handler = new StatusActionHandler(statuses, () => null);

        Assert.False(handler.TryHandle(ActionIds.Cancel));

        Assert.NotNull(statuses.Log.Latest);
    }

    [Fact]
    public void GivenAnotherAction_WhenFallingThrough_ThenItIsNotClaimed()
    {
        var statuses = new StatusChannel();
        statuses.Warning("careful");
        var handler = new StatusActionHandler(statuses, () => null);

        Assert.False(handler.TryHandleFallback(ActionIds.RepositoryRefresh));

        Assert.NotNull(statuses.Log.Latest);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenHandling_ThenItIsNotClaimed()
    {
        var handler = new StatusActionHandler(new StatusChannel(), () => null);

        Assert.False(handler.TryHandle(ActionIds.RepositoryRefresh));
    }

    [Fact]
    public void GivenNoSession_WhenRetrying_ThenNothingToRetry()
    {
        var handler = new StatusActionHandler(new StatusChannel(), () => null);

        Assert.True(handler.TryHandle(ActionIds.DetailRetry));

        Assert.Equal("nothing to retry", handler.Message);
    }

    [Fact]
    public async Task GivenIdleDetails_WhenRetrying_ThenNothingToRetry()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var pump = new UiPump(Token);
        _session = await CreateSessionAsync(pump, testRepository);
        var handler = new StatusActionHandler(new StatusChannel(), () => _session);

        handler.TryHandle(ActionIds.DetailRetry);

        Assert.Equal("nothing to retry", handler.Message);
    }

    [Fact]
    public async Task GivenFailedDetails_WhenRetrying_ThenTheLoadStartsAgain()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var pump = new UiPump(Token);
        _session = await CreateSessionAsync(pump, testRepository);
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));
        _session.Details.Select(missing);
        await pump.RunUntilAsync(() => _session.Details.Current.Status == CommitDetailsStatus.Failed);
        var handler = new StatusActionHandler(new StatusChannel(), () => _session);

        handler.TryHandle(ActionIds.DetailRetry);

        Assert.Equal("retrying...", handler.Message);
        Assert.Equal(CommitDetailsStatus.Loading, _session.Details.Current.Status);
    }

    [Fact]
    public void GivenNoSession_WhenSwitchingTheMergeParent_ThenItSaysThereIsNoMerge()
    {
        var handler = new StatusActionHandler(new StatusChannel(), () => null);

        Assert.True(handler.TryHandle(ActionIds.DiffMergeParent));

        Assert.Equal("Not a merge commit (or still loading)", handler.Message);
    }

    [Fact]
    public async Task GivenALoadedMergeCommit_WhenSwitchingTheMergeParent_ThenTheStatusNamesTheParent()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", new TestFile("main.txt", "main\n"))
            .Merge("side");
        var pump = new UiPump(Token);
        _session = await CreateSessionAsync(pump, testRepository);
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", "HEAD").Trim(), out var merge));
        _session.Details.Select(merge);
        await pump.RunUntilAsync(() => _session.Details.Current.Status == CommitDetailsStatus.Loaded);
        var handler = new StatusActionHandler(new StatusChannel(), () => _session);

        handler.TryHandle(ActionIds.DiffMergeParent);
        await pump.RunUntilAsync(() => _session.Details.Current.Details!.ParentIndex == 1);

        Assert.Equal("Switching to parent 2 of 2...", handler.Message);
        Assert.Equal(["main.txt"], _session.Details.Current.Details!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task GivenAFailedFileTree_WhenRetrying_ThenTheTreeLoadStartsAgain()
    {
        var pump = new UiPump(Token);
        var debounce = TimeSpan.FromMilliseconds(1);
        var reader = new FakeCommitTreeReader();
        var store = new CommitTreeStore(reader, FakeCommitDetailsReader.Location, pump.Post, debounce);
        _session = new TabSession(
            new TabId(1),
            GraphGlyphSet.Unicode,
            new CommitDetailsStore(
                new FakeCommitDetailsReader(),
                FakeCommitDetailsReader.Location,
                pump.Post,
                debounce),
            tree: new FileTreeSession(store));
        var missing = FakeCommitDetailsReader.MissingId();
        store.Select(missing);
        await pump.RunUntilAsync(() => store.Current.Status == CommitDetailsStatus.Failed);
        var handler = new StatusActionHandler(new StatusChannel(), () => _session);

        handler.TryHandle(ActionIds.DetailRetry);

        Assert.Equal("retrying...", handler.Message);
        Assert.Equal(CommitDetailsStatus.Loading, store.Current.Status);
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<TabSession> CreateSessionAsync(UiPump pump, TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        return new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, pump.Post, TimeSpan.FromMilliseconds(1)));
    }
}
