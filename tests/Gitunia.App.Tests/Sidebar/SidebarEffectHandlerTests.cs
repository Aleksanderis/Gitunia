using Gitunia.App.Sidebar;
using Gitunia.App.Tabs;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.Graph;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.App.Tests.Sidebar;

public sealed class SidebarEffectHandlerTests : IDisposable, ITabSessions
{
    private static readonly TabId Tab = new(1);

    private readonly FakeCommitDetailsReader _reader = FakeCommitDetailsReader.Chain(3);
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly TabSession _session;
    private int _expandRequests;

    public SidebarEffectHandlerTests()
    {
        _session = new TabSession(
            new TabSessionParts(
                Tab,
                new CommitDetailsStore(_reader, FakeCommitDetailsReader.Location, InlineUi.Instance, TimeSpan.Zero)));
        _session.Append(
            [.. _reader.Commits.Select(id => new GraphEntry(
                id,
                "x",
                "A",
                DateTimeOffset.UnixEpoch,
                [],
                new GraphRow(id, 0, 1, [])))]);
    }

    [Fact]
    public async Task GivenACommitInTheGraph_WhenLocating_ThenItsIndexIsPosted()
    {
        var handler = new SidebarEffectHandler(this, _dispatcher);

        handler.Locate(new LocateGraphCommitEffect(Tab, _reader.Commits[2], "main"));

        var answer = await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new SidebarCommitLocated(Tab, 2), answer);
        Assert.Equal(0, _expandRequests);
    }

    [Fact]
    public async Task GivenACommitOutsideACompleteGraph_WhenLocating_ThenMissingIsPostedAsFinal()
    {
        _session.Complete();
        var handler = new SidebarEffectHandler(this, _dispatcher);

        handler.Locate(new LocateGraphCommitEffect(Tab, FakeCommitDetailsReader.MissingId(), "feature"));

        var answer = await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new SidebarCommitMissing(Tab, "feature", true), answer);
    }

    [Fact]
    public async Task GivenACommitOutsideAGraphThatIsStillLoading_WhenLocating_ThenTheRestIsAskedForAndMissingSaysSo()
    {
        _session.ExpandRequested += () => _expandRequests++;
        _session.Complete();
        _session.Truncate(100);
        var handler = new SidebarEffectHandler(this, _dispatcher);

        handler.Locate(new LocateGraphCommitEffect(Tab, FakeCommitDetailsReader.MissingId(), "feature"));

        var answer = await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new SidebarCommitMissing(Tab, "feature", false), answer);
        Assert.Equal(1, _expandRequests);
    }

    [Fact]
    public async Task GivenATabWithoutASession_WhenLocating_ThenMissingIsPosted()
    {
        var handler = new SidebarEffectHandler(new NoSessions(), _dispatcher);

        handler.Locate(new LocateGraphCommitEffect(Tab, _reader.Commits[0], "main"));

        var answer = await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new SidebarCommitMissing(Tab, "main", true), answer);
    }

    public TabSession Find(TabId tabId) => _session;

    public void Dispose() => _session.Dispose();

    private sealed class NoSessions : ITabSessions
    {
        public TabSession? Find(TabId tabId) => null;
    }
}
