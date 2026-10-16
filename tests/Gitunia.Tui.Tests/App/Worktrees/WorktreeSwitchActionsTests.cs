using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Worktrees;

public sealed class WorktreeSwitchActionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenTabWithViewState_WhenSwitching_ThenTabMovesToTheWorktreeAndKeepsItsView()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);
        Assert.True(ObjectId.TryParse(repository.Head, out var commit));
        harness.Dispatch(new SelectCommitCommand(commit));
        harness.Dispatch(new ScrollToCommand(3));
        harness.Dispatch(new SetFilterCommand("needle"));
        var tabId = harness.Tab.Id;

        var notice = harness.Execute(ActionIds.WorktreeSwitch);
        await Harness.WaitForAsync(() => harness.Tab.Location.RootPath == path);

        Assert.Equal(string.Empty, notice);
        Assert.Equal(tabId, harness.Tab.Id);
        Assert.Single(harness.Host.Workspace.Tabs);
        Assert.Equal(commit, harness.Tab.View.SelectedCommit);
        Assert.Equal(3, harness.Tab.View.ScrollOffset);
        Assert.Equal("needle", harness.Tab.View.FilterText);
        Assert.True(harness.Tab.Location.IsLinkedWorktree);
        Assert.Equal(path, harness.Tab.CommandPath);
        Assert.Equal(1, harness.Loads.Count(load => load.RepositoryPath == path));
        Assert.Equal($"Switched to worktree {path}", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenTabOnTheWorktree_WhenSwitchingToIt_ThenRefusedWithoutLoad()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var harness = await Harness.LoadAsync(path, path);

        var notice = harness.Execute(ActionIds.WorktreeSwitch);

        Assert.Equal("This tab already shows that worktree", notice);
        Assert.DoesNotContain(harness.Loads, load => load.RepositoryPath != path);
    }

    [Fact]
    public async Task GivenAnotherTabShowingTheWorktree_WhenSwitching_ThenRefusedPointingToOpen()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);
        await harness.OpenTabAsync(path);
        var mainTab = harness.Host.Workspace.FindTabByRootPath(repository.WorkingTreePath)!;
        harness.Host.Dispatch(new ActivateTabCommand(mainTab.Id));

        var notice = harness.Execute(ActionIds.WorktreeSwitch);

        Assert.Contains("already open in tab", notice, StringComparison.Ordinal);
        Assert.Contains("<space>wt", notice, StringComparison.Ordinal);
        Assert.Equal(repository.WorkingTreePath, harness.Tab.Location.RootPath);
    }

    [Fact]
    public async Task GivenPrunableWorktree_WhenSwitching_ThenRefusedPointingToPrune()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out var prunablePath, out _);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, prunablePath);

        var notice = harness.Execute(ActionIds.WorktreeSwitch);

        Assert.Contains("<space>wp", notice, StringComparison.Ordinal);
        Assert.Equal(repository.WorkingTreePath, harness.Tab.Location.RootPath);
    }

    [Fact]
    public async Task GivenNoWorktreeRow_WhenSwitching_ThenAskedToSelectOne()
    {
        using var repository = TestRepositories.WithWorktree(out _);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);

        var notice = harness.Execute(ActionIds.WorktreeSwitch);

        Assert.Equal("Select a worktree in the sidebar first", notice);
    }

    [Fact]
    public async Task GivenOperationRunningOnTheTab_WhenSwitching_ThenRefusedAsBusy()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);
        harness.Gate.Begin(harness.Tab.Id);

        var notice = harness.Execute(ActionIds.WorktreeSwitch);

        Assert.StartsWith("Busy:", notice, StringComparison.Ordinal);
        Assert.Equal(repository.WorkingTreePath, harness.Tab.Location.RootPath);
    }

    [Fact]
    public async Task GivenWorktree_WhenOpening_ThenItGetsItsOwnTabAndOpeningAgainFocusesIt()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);
        var mainId = harness.Tab.Id;

        var notice = harness.Execute(ActionIds.WorktreeOpen);
        await Harness.WaitForAsync(() => harness.Host.Workspace.Tabs.Length == 2);

        Assert.Equal(string.Empty, notice);
        Assert.Equal(path, harness.Tab.Location.RootPath);
        Assert.NotEqual(mainId, harness.Tab.Id);
        Assert.Equal(repository.WorkingTreePath, harness.Host.Workspace.FindTab(mainId)!.Location.RootPath);

        harness.Host.Dispatch(new ActivateTabCommand(mainId));
        harness.Execute(ActionIds.WorktreeOpen);
        await Harness.WaitForAsync(() => harness.Host.Workspace.ActiveTabId != mainId);

        Assert.Equal(2, harness.Host.Workspace.Tabs.Length);
        Assert.Equal(path, harness.Tab.Location.RootPath);
    }

    [Fact]
    public async Task GivenUnrelatedAction_WhenExecuting_ThenNotHandled()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        Assert.False(harness.Actions.TryExecute(ActionIds.WorktreeAdd));
    }

    private sealed class Harness
    {
        private Harness(string? rowPath)
        {
            Row = rowPath is null
                ? null
                : new SidebarRow(
                    SidebarRowKind.Worktree,
                    SidebarSection.Worktrees,
                    Path.GetFileName(rowPath),
                    string.Empty,
                    null,
                    false,
                    false,
                    0,
                    null,
                    null,
                    rowPath);
            Host.EffectRaised += effect =>
            {
                if (effect is LoadRepositoryEffect load)
                {
                    Loads.Add(load);
                }
            };
            Actions = new WorktreeSwitchActions(
                Host,
                TestGit.Services.Discovery,
                Statuses,
                InlineUi.Post,
                _ => CancellationToken.None,
                Gate,
                () => Row);
        }

        public WorkspaceHost Host { get; } = new(Workspace.Empty);

        public WorktreeSwitchActions Actions { get; }

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public List<LoadRepositoryEffect> Loads { get; } = [];

        public SidebarRow? Row { get; }

        public RepositoryState Tab => Host.Workspace.ActiveTab!;

        public static async Task<Harness> LoadAsync(string tabPath, string? rowPath)
        {
            var harness = new Harness(rowPath);
            await harness.OpenTabAsync(tabPath);
            return harness;
        }

        public async Task OpenTabAsync(string path)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(path, Token);
            Host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = Host.Workspace.ActiveTabId!.Value;
            await ReloadAsync(tabId, location);
        }

        public void Dispatch(RepositoryCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

        public string Execute(ActionId action)
        {
            Actions.TryExecute(action);
            return Actions.Notice;
        }

        public static async Task WaitForAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1, Token);
            }

            Assert.True(condition());
        }

        // What the tab's load does in the app: fills the worktree list the action looks the selected row up in.
        private async Task ReloadAsync(TabId tabId, RepositoryLocation location)
        {
            var details = new CommitDetailsStore(
                TestGit.Services.CommitDetails,
                location,
                action => action(),
                TimeSpan.Zero);
            var session = new TabSession(tabId, GraphGlyphSet.Unicode, details);
            await TestGit.Services.RepositoryLoader
                .RunAsync(location, session, Host, new StatusChannel(), InlineUi.Post, Token);
        }
    }
}
