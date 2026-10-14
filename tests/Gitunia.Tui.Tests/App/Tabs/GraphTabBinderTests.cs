using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs;

public class GraphTabBinderTests
{
    [Fact]
    public async Task GivenSelectedCommit_WhenTheGraphIsReplacedWithNewCommitsOnTop_ThenTheSameCommitStaysSelected()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", new TestFile("a.txt", "1")).Commit("two", new TestFile("a.txt", "2")).Commit("three", new TestFile("a.txt", "3"));
        using var harness = await Harness.LoadAsync(repository);
        var selected = harness.Session.Graph[2].Id;
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(selected)));
        harness.Graph.SelectedItem = 2;

        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        repository.Commit("four", new TestFile("a.txt", "4"));
        harness.Effects.Refresh(harness.TabId);
        await harness.Pump.RunUntilAsync(() => replaced == 1);

        Assert.Equal(3, harness.Graph.SelectedItem);
        Assert.Equal(selected, harness.Session.Graph[harness.Graph.SelectedItem!.Value].Id);
        Assert.Equal(selected, harness.Host.Workspace.FindTab(harness.TabId)!.View.SelectedCommit);
    }

    [Fact]
    public async Task GivenSelectedCommit_WhenItDisappearsFromTheGraph_ThenTheNearestRowIsSelectedAndSaved()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", new TestFile("a.txt", "1")).Commit("two", new TestFile("a.txt", "2")).Commit("three", new TestFile("a.txt", "3"));
        using var harness = await Harness.LoadAsync(repository);
        var selected = harness.Session.Graph[0].Id;
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(selected)));
        harness.Graph.SelectedItem = 0;

        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        repository.Git("reset", "--hard", "HEAD~1");
        harness.Effects.Refresh(harness.TabId);
        await harness.Pump.RunUntilAsync(() => replaced == 1);

        var current = harness.Session.Graph[harness.Graph.SelectedItem!.Value].Id;
        Assert.NotEqual(selected, current);
        Assert.Equal(current, harness.Host.Workspace.FindTab(harness.TabId)!.View.SelectedCommit);
    }

    [Fact]
    public async Task GivenScrolledList_WhenCommitsArriveOnTop_ThenSelectionKeepsItsScreenRow()
    {
        using var repository = TestRepository.Create();
        for (var index = 0; index < 30; index++)
        {
            repository.Commit("commit " + index, new TestFile("a.txt", index.ToString()));
        }

        using var harness = await Harness.LoadAsync(repository);
        var selected = harness.Session.Graph[15].Id;
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(selected)));
        harness.Graph.SelectedItem = 15;
        harness.Graph.Viewport = harness.Graph.Viewport with { Y = 10 };
        var rowBefore = 15 - harness.Graph.Viewport.Y;
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;

        repository.Commit("new", new TestFile("a.txt", "new"));
        harness.Effects.Refresh(harness.TabId);
        await harness.Pump.RunUntilAsync(() => replaced == 1);

        Assert.Equal(16, harness.Graph.SelectedItem);
        Assert.Equal(rowBefore, harness.Graph.SelectedItem - harness.Graph.Viewport.Y);
    }

    private sealed class Harness : IDisposable
    {
        private Harness(WorkspaceHost host, EffectRunner effects, TabId tabId, ListView graph, UiPump pump)
        {
            Pump = pump;
            Host = host;
            Effects = effects;
            TabId = tabId;
            Graph = graph;
        }

        public UiPump Pump { get; }

        public WorkspaceHost Host { get; }

        public EffectRunner Effects { get; }

        public TabId TabId { get; }

        public ListView Graph { get; }

        public TabSession Session => Effects.SessionOf(TabId)!;

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var runner = new GitRunner();
            var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
            var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
            var tabId = update.Workspace.Tabs[0].Id;
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new WorkspaceHost(Workspace.Empty);
            var effects = new EffectRunner(runner, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel());
            var graph = new ListView { Width = 40, Height = 10 };
            var binder = new GraphTabBinder(host, effects, graph, new GraphListSource(new CommitGraph(), GraphGlyphSet.Unicode), () => { });
            host.Changed += binder.Sync;
            host.Restore(update);
            binder.Sync();
            var harness = new Harness(host, effects, tabId, graph, pump);
            await pump.RunUntilAsync(() => !harness.Session.IsLoading);
            return harness;
        }

        public void Dispose() => Effects.Dispose();
    }
}
