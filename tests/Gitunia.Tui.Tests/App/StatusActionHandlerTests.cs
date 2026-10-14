using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class StatusActionHandlerTests
{
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
        using var session = await CreateSessionAsync(testRepository);
        var handler = new StatusActionHandler(new StatusChannel(), () => session);

        handler.TryHandle(ActionIds.DetailRetry);

        Assert.Equal("nothing to retry", handler.Message);
    }

    [Fact]
    public async Task GivenFailedDetails_WhenRetrying_ThenTheLoadStartsAgain()
    {
        using var testRepository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        using var session = await CreateSessionAsync(testRepository);
        Assert.True(ObjectId.TryParse(new string('1', 40), out var missing));
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Details.Changed += () =>
        {
            if (session.Details.Current.Status == CommitDetailsStatus.Failed)
            {
                settled.TrySetResult();
            }
        };
        session.Details.Select(missing);
        await settled.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        var handler = new StatusActionHandler(new StatusChannel(), () => session);

        handler.TryHandle(ActionIds.DetailRetry);

        Assert.Equal("retrying...", handler.Message);
        Assert.Equal(CommitDetailsStatus.Loading, session.Details.Current.Status);
    }

    private static async Task<TabSession> CreateSessionAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        return new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.FromMilliseconds(1)));
    }
}
