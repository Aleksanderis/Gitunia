using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Tests.Fakes;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class StagingActionsTests
{
    [Fact]
    public async Task GivenAFileUnderTheCursor_WhenStaging_ThenItsPathIsStagedAndTheCursorStaysAtTheIndex()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"), Modified("c.txt"));
        harness.Select(WorkingTreeSection.Unstaged, 1);
        harness.OnRefresh = () => harness.Load(Modified("a.txt"), Modified("c.txt"), StagedModified("b.txt"));

        harness.Run(ActionIds.WorkingTreeStage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["b.txt"], Assert.Single(harness.Writer.Staged));
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "c.txt", 1), harness.Selection);
        Assert.Equal("Staged b.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAStagedRename_WhenUnstaging_ThenBothPathsAreSent()
    {
        using var harness = new Harness(StagedRename("old.txt", "new.txt"));
        harness.Focus(PaneKind.WorkingStaged);

        harness.Run(ActionIds.WorkingTreeUnstage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["new.txt", "old.txt"], Assert.Single(harness.Writer.Unstaged));
        Assert.Equal([false], harness.Writer.UnbornFlags);
    }

    [Fact]
    public async Task GivenAnUnbornBranch_WhenUnstaging_ThenTheWriterIsToldThereIsNoHead()
    {
        using var harness = new Harness(StagedAdded("a.txt"));
        harness.Load(HeadKind.Unborn, StagedAdded("a.txt"));
        harness.Focus(PaneKind.WorkingStaged);

        harness.Run(ActionIds.WorkingTreeUnstage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal([true], harness.Writer.UnbornFlags);
    }

    [Fact]
    public async Task GivenConflictedFiles_WhenStagingAll_ThenTheyAreSkippedAndCounted()
    {
        using var harness = new Harness(Conflict("c.txt"), Modified("a.txt"), Untracked("n.txt"));

        harness.Run(ActionIds.WorkingTreeStageAll);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["a.txt", "n.txt"], Assert.Single(harness.Writer.Staged));
        Assert.Equal("Staged 2 files (1 conflicted skipped)", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public void GivenAConflictedFile_WhenStagingIt_ThenItIsRefusedWithoutRunningGit()
    {
        using var harness = new Harness(Conflict("c.txt"));

        var notice = harness.Run(ActionIds.WorkingTreeStage);

        Assert.Contains("conflicted", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Writer.Staged);
    }

    [Fact]
    public async Task GivenAnOperationInProgress_WhenAnotherArrives_ThenItIsRefusedAsBusy()
    {
        using var harness = new Harness(Modified("a.txt"), Modified("b.txt"));
        harness.Writer.IsHeld = true;

        Assert.Equal(string.Empty, harness.Run(ActionIds.WorkingTreeStage));
        var notice = harness.Run(ActionIds.WorkingTreeStage);
        harness.Writer.Release();
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Contains("Busy", notice, StringComparison.Ordinal);
        Assert.Single(harness.Writer.Staged);
    }

    [Fact]
    public async Task GivenGitFails_WhenStaging_ThenTheErrorIsShownAndTheStatusIsReRead()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Writer.Failure = new InvalidOperationException("index.lock exists");
        var refreshed = false;
        harness.OnRefresh = () => refreshed = true;

        harness.Run(ActionIds.WorkingTreeStage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.True(refreshed);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("index.lock exists", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheStagedList_WhenStaging_ThenItSaysWhereStagingWorks()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Focus(PaneKind.WorkingStaged);

        var notice = harness.Run(ActionIds.WorkingTreeStage);

        Assert.Contains("Unstaged list", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Writer.Staged);
    }

    [Fact]
    public void GivenNothingToUnstage_WhenUnstagingAll_ThenNothingRuns()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Focus(PaneKind.WorkingStaged);

        var notice = harness.Run(ActionIds.WorkingTreeUnstageAll);

        Assert.Equal("Nothing to do", notice);
        Assert.Empty(harness.Writer.Unstaged);
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);

        public Harness(params StatusEntry[] entries)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-staging-actions-tests");
            var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);
            _host.Dispatch(new OpenRepositoryCommand(location));
            Load(entries);
            Send(new OpenWorkingTreeCommand());
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Staging = new StagingActions(_host, Writer, Statuses, Pump.Post, RefreshStatus, _ => TestContext.Current.CancellationToken);
        }

        public UiPump Pump { get; }

        public FakeIndexWriter Writer { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public StagingActions Staging { get; }

        /// <summary>Stands in for the tab refresher: runs when a status read is requested, before the completion callback.</summary>
        public Action OnRefresh { get; set; } = () => { };

        public RepositoryState Tab => _host.Workspace.ActiveTab!;

        public WorkingTreeSelection Selection => Tab.WorkingTree!.Selection!;

        public string Run(ActionId action) => Staging.Execute(Tab, action);

        public void Load(params StatusEntry[] entries) => Send(new WorkingTreeLoadedCommand(Snapshot(entries)));

        public void Load(HeadKind head, params StatusEntry[] entries)
        {
            var snapshot = Snapshot(entries);
            var branch = snapshot.Branch with { Head = new HeadState(head, null, null) };
            Send(new WorkingTreeLoadedCommand(snapshot with { Branch = branch }));
        }

        public void Select(WorkingTreeSection section, int index)
        {
            Send(new SelectWorkingTreeFileCommand(section, index));
        }

        public void Focus(PaneKind pane) => Send(new FocusPaneCommand(pane));

        public void Dispose() => _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new LeaveWorkingTreeCommand()));

        private bool RefreshStatus(TabId tabId, Action done)
        {
            Pump.Post(() =>
            {
                OnRefresh();
                done();
            });
            return true;
        }

        private void Send(RepositoryCommand command) =>
            _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, command));
    }
}
