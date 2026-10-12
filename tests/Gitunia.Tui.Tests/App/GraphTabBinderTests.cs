using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class GraphTabBinderTests
{
    [Fact]
    public async Task GivenSelectedCommit_WhenTheGraphIsReplacedWithNewCommitsOnTop_ThenTheSameCommitStaysSelected()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", ("a.txt", "1")).Commit("two", ("a.txt", "2")).Commit("three", ("a.txt", "3"));
        using var harness = await Harness.LoadAsync(repository);
        var selected = harness.Session.Graph[2].Id;
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(selected)));
        harness.Graph.SelectedItem = 2;

        repository.Commit("four", ("a.txt", "4"));
        harness.Effects.Refresh(harness.TabId);
        await WaitUntilAsync(() => harness.Session.Graph.Count == 4);

        Assert.Equal(3, harness.Graph.SelectedItem);
        Assert.Equal(selected, harness.Session.Graph[harness.Graph.SelectedItem!.Value].Id);
        Assert.Equal(selected, harness.Host.Workspace.FindTab(harness.TabId)!.View.SelectedCommit);
    }

    [Fact]
    public async Task GivenSelectedCommit_WhenItDisappearsFromTheGraph_ThenTheNearestRowIsSelectedAndSaved()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", ("a.txt", "1")).Commit("two", ("a.txt", "2")).Commit("three", ("a.txt", "3"));
        using var harness = await Harness.LoadAsync(repository);
        var selected = harness.Session.Graph[0].Id;
        harness.Host.Dispatch(new TabCommand(harness.TabId, new SelectCommitCommand(selected)));
        harness.Graph.SelectedItem = 0;

        repository.Git("reset", "--hard", "HEAD~1");
        harness.Effects.Refresh(harness.TabId);
        await WaitUntilAsync(() => harness.Session.Graph.Count == 2);

        var current = harness.Session.Graph[harness.Graph.SelectedItem!.Value].Id;
        Assert.NotEqual(selected, current);
        Assert.Equal(current, harness.Host.Workspace.FindTab(harness.TabId)!.View.SelectedCommit);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class Harness : IDisposable
    {
        private Harness(WorkspaceHost host, EffectRunner effects, TabId tabId, ListView graph)
        {
            Host = host;
            Effects = effects;
            TabId = tabId;
            Graph = graph;
        }

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
            var host = new WorkspaceHost(Workspace.Empty);
            var effects = new EffectRunner(runner, host, InlineUi.Post, GraphGlyphSet.Unicode, new StatusChannel());
            var graph = new ListView { Width = 40, Height = 10 };
            var binder = new GraphTabBinder(host, effects, graph, new GraphListSource(new CommitGraph(), GraphGlyphSet.Unicode), () => { });
            host.Changed += binder.Sync;
            host.Restore(update);
            binder.Sync();
            var harness = new Harness(host, effects, tabId, graph);
            await WaitUntilAsync(() => !harness.Session.IsLoading);
            return harness;
        }

        public void Dispose() => Effects.Dispose();
    }
}
