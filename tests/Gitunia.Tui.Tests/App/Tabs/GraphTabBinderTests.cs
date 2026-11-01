using Gitunia.Core;
using Gitunia.App;
using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
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
    public async Task GivenSelectedCommit_WhenTheGraphIsReplaced_ThenTheBinderReportsReplacingOnlyWhileItMovesTheRow()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", new TestFile("a.txt", "1")).Commit("two", new TestFile("a.txt", "2"));
        using var harness = await Harness.LoadAsync(repository);
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(harness.Session.Graph[1].Id)));
        harness.Graph.SelectedItem = 1;
        Assert.False(harness.Binder.IsReplacing);

        var replaced = 0;
        var replacingDuringMove = false;
        var binder = harness.Binder;
        harness.Graph.ValueChanged += (_, _) => replacingDuringMove |= binder.IsReplacing;
        harness.Session.GraphReplaced += () => replaced++;
        repository.Commit("three", new TestFile("a.txt", "3"));
        harness.Effects.Refresh(harness.TabId);
        await harness.Pump.RunUntilAsync(() => replaced == 1);

        Assert.True(replacingDuringMove);
        Assert.False(harness.Binder.IsReplacing);
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
            repository.Commit($"commit {index}", new TestFile("a.txt", index.ToString()));
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

    [Fact]
    public async Task GivenSelectedCommit_WhenTheTabSwitchesWorktree_ThenTheNewSessionIsShownAndTheCommitStaysSelected()
    {
        using var repository = TestRepositories.WithWorktree(out var worktreePath);
        using var harness = await Harness.LoadAsync(repository);
        var before = harness.Session;
        var selected = before.Graph[1].Id;
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(selected)));
        harness.Graph.SelectedItem = 1;
        var location = await TestGit.Services.Discovery.DiscoverAsync(worktreePath, CancellationToken.None);

        harness.Host.Dispatch(new SwitchWorktreeCommand(harness.TabId, location));
        await harness.Pump.RunUntilAsync(() => !harness.Session.IsLoading);

        Assert.NotSame(before, harness.Session);
        Assert.Same(harness.Session.Source, harness.Graph.Source);
        Assert.Equal(selected, harness.Session.Graph[harness.Graph.SelectedItem!.Value].Id);
        Assert.Equal(selected, harness.Host.Workspace.FindTab(harness.TabId)!.View.SelectedCommit);
        Assert.Equal(worktreePath, harness.Host.Workspace.FindTab(harness.TabId)!.Location.RootPath);
    }

    private sealed class Harness : IDisposable
    {
        private Harness(
            Dispatcher host,
            EffectRunner<TerminalTabSession> effects,
            TabId tabId,
            ListView graph,
            UiPump pump,
            GraphTabBinder binder)
        {
            Pump = pump;
            Host = host;
            Effects = effects;
            TabId = tabId;
            Graph = graph;
            Binder = binder;
        }

        public UiPump Pump { get; }

        public Dispatcher Host { get; }

        public EffectRunner<TerminalTabSession> Effects { get; }

        public TabId TabId { get; }

        public ListView Graph { get; }

        public GraphTabBinder Binder { get; }

        public TerminalTabSession Session => Effects.SessionOf(TabId)!;

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
            var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
            var tabId = update.Workspace.Tabs[0].Id;
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty);
            var effects = new EffectRunner<TerminalTabSession>(TestGit.Services, host, pump.Post, new TerminalTabSessionFactory(GraphGlyphSet.Unicode), new StatusChannel());
            var graph = new ListView { Width = 40, Height = 10 };
            var binder = new GraphTabBinder(host, effects, graph, new GraphListSource(new CommitGraph(), GraphGlyphSet.Unicode), () => { });
            host.Changed += binder.Sync;
            host.Restore(update);
            binder.Sync();
            var harness = new Harness(host, effects, tabId, graph, pump, binder);
            await pump.RunUntilAsync(() => !harness.Session.IsLoading);
            return harness;
        }

        public void Dispose() => Effects.Dispose();
    }
}
