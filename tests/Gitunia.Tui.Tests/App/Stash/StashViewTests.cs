using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Stash;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;

namespace Gitunia.Tui.Tests.App.Stash;

public class StashViewTests
{
    [Fact]
    public async Task GivenAStashRow_WhenPressingEnter_ThenTheChangesTabListsTrackedAndUntrackedFilesAndTheDiffHasBoth()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);

        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        Assert.Equal("stash@{0}", StashViewController.ShownName(harness.Session));
        var rows = harness.ChangesRows();
        Assert.Equal("Stash stash@{0}: 2 files changed, 1 untracked", rows[0]);
        Assert.Equal(["M file.txt", "? untracked.txt"], rows[1..]);
        var commit = harness.Session.Details.Current.Commit!.Value;
        harness.Diff.Load(commit, null);
        await harness.WaitForDiffAsync();
        Assert.Equal(["file.txt", "untracked.txt"], harness.Diff.Document.FilePaths.Select(paths => paths[0]));
    }

    [Fact]
    public async Task GivenAShownStash_WhenACommitActionAsksForItsTarget_ThenItGetsNoCommit()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        var graphCommit = harness.Session.Graph[0].Id.ToString();
        Assert.Equal(graphCommit, harness.Controller.ActionTarget(graphCommit));

        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        Assert.Null(harness.Controller.ActionTarget(graphCommit));
    }

    [Fact]
    public async Task GivenAShownStash_WhenTheBinderMovesTheGraphSelection_ThenTheViewStaysButTheUsersMoveEndsIt()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        Assert.False(harness.Controller.KeepsViewOnSelectionChange(true));
        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        Assert.True(harness.Controller.KeepsViewOnSelectionChange(true));
        Assert.False(harness.Controller.KeepsViewOnSelectionChange(false));
    }

    [Fact]
    public async Task GivenOnlyAnUntrackedFile_WhenShowingTheStash_ThenTheFileListMarksItUntracked()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("only.txt", "x\n");
        repository.Git("stash", "push", "-q", "--include-untracked");
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);

        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        Assert.Equal(["? only.txt"], harness.ChangesRows()[1..]);
    }

    [Fact]
    public async Task GivenAStashRow_WhenPressingL_ThenNothingIsShownAndTheFocusStays()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.DetailVisible = true;

        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarExpand, null);

        Assert.False(harness.Controller.IsShowing);
        Assert.Equal(PaneKind.Sidebar, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenTheStashLeftTheStackBeforeItLoaded_WhenShowingIt_ThenThePaneSaysSoAndNoOtherStashAppears()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.Select("stash@{0}");
        repository.Git("stash", "drop", "-q");

        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.Pump.RunUntilAsync(() => harness.Session.Details.Current.Status == CommitDetailsStatus.Failed);

        Assert.Equal(StashAwareCommitDetailsReader.GoneMessage, harness.Session.Details.Current.Error);
        Assert.Contains("Could not load the stash", harness.ChangesRows()[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAShownStash_WhenTheStackNoLongerHasIt_ThenTheViewEndsWithAWarningAndTheGraphReturns()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        harness.Send(new StashesLoadedCommand([]));

        Assert.Null(StashViewController.ShownName(harness.Session));
        Assert.Equal("stash@{0} is no longer in the stash list", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest.Severity);
        Assert.Equal(1, harness.Reselects);
    }

    [Fact]
    public async Task GivenAShownStashWasDroppedByTheApp_WhenTheStackReloads_ThenTheRestoreHintStaysInTheStatusBar()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();
        harness.Statuses.Post("Dropped stash@{0} (abc); undo: git stash store -m wip abc");

        harness.Send(new StashesLoadedCommand([]));

        Assert.Null(StashViewController.ShownName(harness.Session));
        Assert.StartsWith("Dropped stash@{0}", harness.Statuses.Current!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAShownStash_WhenANewStashPushesItDown_ThenThePaneKeepsThatStashUnderItsNewName()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();
        var shown = harness.Tab.Stashes[0];
        var newer = new StashEntry(0, "stash@{0}", "feedface", "On main: newer");

        harness.Send(new StashesLoadedCommand([newer, shown with { Index = 1, Name = "stash@{1}" }]));

        Assert.Equal("stash@{1}", StashViewController.ShownName(harness.Session));
        Assert.Equal(shown.CommitHash, harness.Session.Details.Current.Commit.ToString());
    }

    [Fact]
    public async Task GivenAShownStash_WhenTheCursorMovesToAnotherStash_ThenThePaneFollowsIt()
    {
        using var repository = TestRepositories.WithStash();
        repository.WriteFile("file.txt", "second\n");
        repository.Git("stash", "push", "-q", "-m", "second");
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        harness.Handler.TryHandle(ActionIds.ListDown, null);

        Assert.Equal("stash@{1}", StashViewController.ShownName(harness.Session));
        Assert.Equal(harness.Tab.Stashes[1].CommitHash, harness.Session.Details.Current.Commit.ToString());
    }

    [Fact]
    public async Task GivenAStashIsShown_WhenActivatingABranchRow_ThenTheStashViewEnds()
    {
        using var repository = TestRepositories.WithStash();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath);
        harness.Select("stash@{0}");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);
        await harness.WaitForDetailsAsync();

        harness.Select("main");
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);

        Assert.Null(StashViewController.ShownName(harness.Session));
        Assert.Equal(1, harness.Reselects);
    }

    private sealed class Harness : IDisposable
    {
        private readonly SidebarListSource _source = new(GraphGlyphSet.Unicode);

        private Harness(WorkspaceHost host, RepositoryLocation location)
        {
            Host = host;
            var tabId = host.Workspace.ActiveTabId!.Value;
            var view = new StashViewSession();
            Diff = new DiffStore(
                new StashAwareDiffReader(TestGit.Services.Diff, TestGit.Services.StashDetails, view.Registry),
                location,
                Pump.Post);
            Session = new TerminalTabSession(
                new TabSessionParts(
                    tabId,
                    new CommitDetailsStore(
                    new StashAwareCommitDetailsReader(
                        TestGit.Services.CommitDetails,
                        TestGit.Services.StashDetails,
                        view.Registry),
                    location,
                    Pump.Post,
                    TimeSpan.Zero),
                    StashView: view),
                GraphGlyphSet.Unicode);
            Controller = new StashViewController(host, () => Session, () => Reselects++, () => { }, Statuses);
            Handler = new SidebarActionHandler(
                host,
                _source,
                () => Session,
                () => 10,
                () => true,
                () => DetailVisible,
                _ => { },
                _ => { })
            {
                StashView = Controller,
            };
            host.Changed += () => _source.Update(host.Workspace.ActiveTab);
            host.Changed += Controller.Reconcile;
            _source.Update(host.Workspace.ActiveTab);
        }

        public UiPump Pump { get; } = new(TestContext.Current.CancellationToken);

        public WorkspaceHost Host { get; }

        public TerminalTabSession Session { get; }

        public DiffStore Diff { get; }

        public StashViewController Controller { get; }

        public SidebarActionHandler Handler { get; }

        public StatusChannel Statuses { get; } = new();

        public bool DetailVisible { get; set; }

        public int Reselects { get; private set; }

        public RepositoryState Tab => Host.Workspace.ActiveTab!;

        public static async Task<Harness> LoadAsync(string repositoryPath)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(repositoryPath, CancellationToken.None);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var harness = new Harness(host, location);
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                harness.Session,
                host,
                new StatusChannel(),
                InlineUi.Post,
                harness.Session.Cancellation.Token);

            // Stashes is a folded section until opened.
            harness.Select("Stashes");
            harness.Handler.TryHandle(ActionIds.SidebarExpand, null);

            return harness;
        }

        public void Select(string label)
        {
            Send(new FocusPaneCommand(PaneKind.Sidebar));
            Send(new SetSidebarCursorCommand(_source.Rows.ToList().FindIndex(row => row.Label == label)));
        }

        public void Send(RepositoryCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

        public Task WaitForDetailsAsync() => Pump.RunUntilAsync(
            () => Session.Details.Current.Status == CommitDetailsStatus.Loaded);

        public Task WaitForDiffAsync() => Pump.RunUntilAsync(() => Diff.Status == DiffStatus.Loaded);

        /// <summary>The Changes tab's rows as text: the heading, then one line per file.</summary>
        public string[] ChangesRows()
        {
            var entry = StashViewController.ShownEntry(Session);
            var content = DetailContentBuilder.Build(
                entry,
                Session.Details.Current,
                BottomTab.Changes,
                stashName: StashViewController.ShownName(Session));

            return [.. content.Rows.Select(row => string.Concat(row.Segments.Select(segment => segment.Text)))];
        }

        public void Dispose()
        {
            Session.Dispose();
            Diff.Dispose();
        }
    }
}
