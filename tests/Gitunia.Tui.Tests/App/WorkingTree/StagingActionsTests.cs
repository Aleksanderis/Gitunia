using Gitunia.App;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;
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
        harness.ReloadOnRefresh(Modified("a.txt"), Modified("c.txt"), StagedModified("b.txt"));

        harness.Run(ActionIds.WorkingTreeStage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["b.txt"], Assert.Single(harness.Writer.Staged));
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "c.txt", 1), harness.Selection);
        Assert.Equal("Staged b.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenTheTreeStyleAndAFolderRow_WhenStaging_ThenEveryFileUnderItIsStagedAndOthersAreNot()
    {
        using var harness = new Harness(Modified("src/a.cs"), Modified("src/deep/b.cs"), Modified("top.txt"));
        harness.UseTree();
        harness.Select(WorkingTreeSection.Unstaged, 0);
        Assert.True(harness.Selection.IsFolder);

        harness.Run(ActionIds.WorkingTreeStage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["src/a.cs", "src/deep/b.cs"], Assert.Single(harness.Writer.Staged));
        Assert.Equal("Staged src/ (2 files)", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAClosedFolderInTheTreeStyle_WhenStagingIt_ThenTheHiddenFilesAreStaged()
    {
        using var harness = new Harness(Modified("src/a.cs"), Modified("src/b.cs"));
        harness.UseTree();
        harness.Select(WorkingTreeSection.Unstaged, 0);
        harness.Send(new ToggleWorkingFolderCommand(WorkingTreeSection.Unstaged, "src", true));
        Assert.Equal(1, harness.Tab.WorkingTree!.RowsOf(WorkingTreeSection.Unstaged).Count);

        harness.Run(ActionIds.WorkingTreeStage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["src/a.cs", "src/b.cs"], Assert.Single(harness.Writer.Staged));
    }

    [Fact]
    public async Task GivenATreeRangeFromAFolderRowToItsFirstFile_WhenStaging_ThenEachFileIsStagedOnce()
    {
        using var harness = new Harness(Modified("src/a.cs"), Modified("src/b.cs"), Modified("z.txt"));
        harness.UseTree();

        harness.RunRange(ActionIds.WorkingTreeStage, 0, 1);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["src/a.cs", "src/b.cs"], Assert.Single(harness.Writer.Staged));
    }

    [Fact]
    public async Task GivenTheTreeStyleAndAFileRow_WhenStaging_ThenTheFileOfThatRowNotTheRowNumberIsStaged()
    {
        using var harness = new Harness(Modified("a/x.cs"), Modified("b/y.cs"), Modified("c.txt"));
        harness.UseTree();
        harness.Select(WorkingTreeSection.Unstaged, 3);
        Assert.Equal("b/y.cs", harness.Selection.Path);

        harness.Run(ActionIds.WorkingTreeStage);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["b/y.cs"], Assert.Single(harness.Writer.Staged));
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
    public async Task GivenThreeConflictedFiles_WhenTheEditorStagesOne_ThenItIsStagedAndTwoAreReportedLeft()
    {
        using var harness = new Harness(Conflict("a.txt"), Conflict("b.txt"), Conflict("c.txt"));
        harness.Select(WorkingTreeSection.Unstaged, 2);
        harness.ReloadOnRefresh(Conflict("a.txt"), Conflict("b.txt"), StagedModified("c.txt"));

        var notice = harness.Staging.StageResolvedFor(harness.Tab, "c.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Empty(notice);
        Assert.Equal(["c.txt"], Assert.Single(harness.Writer.Staged));
        Assert.Equal("Resolved and staged c.txt (Staged list), 2 conflicts left", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(2, harness.Tab.WorkingTree!.Unstaged.Count(entry => entry.Kind == StatusEntryKind.Unmerged));
        Assert.Equal("a.txt", harness.Selection.Path);
        Assert.Equal(WorkingTreeSection.Unstaged, harness.Selection.Section);
    }

    [Fact]
    public async Task GivenTheLastConflictedFile_WhenTheEditorStagesIt_ThenTheStatusSaysAllAreResolved()
    {
        using var harness = new Harness(Conflict("c.txt"));
        harness.ReloadOnRefresh(StagedModified("c.txt"));

        harness.Staging.StageResolvedFor(harness.Tab, "c.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal("Resolved and staged c.txt (Staged list): all conflicts resolved, commit it", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(
            new WorkingTreeSelection(WorkingTreeSection.Staged, "c.txt", 0),
            harness.Selection);
    }

    [Fact]
    public async Task GivenAModifiedFileStaysUnstaged_WhenTheEditorStagesTheLastConflict_ThenTheStagedListIsFocused()
    {
        using var harness = new Harness(Conflict("c.txt"), Modified("m.txt"));
        harness.ReloadOnRefresh(Modified("m.txt"), StagedModified("c.txt"));

        harness.Staging.StageResolvedFor(harness.Tab, "c.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
        Assert.Equal("c.txt", harness.Selection.Path);
    }

    [Fact]
    public async Task GivenTheMiddleOfThreeConflictedFiles_WhenTheEditorStagesIt_ThenTheCursorMovesToTheFirstLeftNotTheSameIndex()
    {
        using var harness = new Harness(Conflict("a.txt"), Conflict("b.txt"), Conflict("c.txt"));
        harness.Select(WorkingTreeSection.Unstaged, 1);
        harness.ReloadOnRefresh(Conflict("a.txt"), Conflict("c.txt"), StagedModified("b.txt"));

        harness.Staging.StageResolvedFor(harness.Tab, "b.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "a.txt", 0), harness.Selection);
    }

    [Fact]
    public async Task GivenTheLastConflictedFileDuringAnOperation_WhenTheEditorStagesIt_ThenTheStatusHintsAtContinuing()
    {
        using var harness = new Harness(Conflict("c.txt"));
        harness.Send(new OperationLoadedCommand(
            new OperationInfo(RepositoryOperation.Rebase, "x", 1, 1, null, null, null, null)));
        harness.ReloadOnRefresh(StagedModified("c.txt"));

        harness.Staging.StageResolvedFor(harness.Tab, "c.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(
            "Resolved and staged c.txt (Staged list): all conflicts resolved, <space>gc continue",
            harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenGitFails_WhenTheEditorStagesTheSavedFile_ThenTheErrorIsShownAndNoResolvedReportFollows()
    {
        using var harness = new Harness(Conflict("a.txt"), Conflict("b.txt"));
        harness.Select(WorkingTreeSection.Unstaged, 1);
        harness.Writer.Failure = new InvalidOperationException("index.lock exists");
        var refreshed = false;
        harness.OnRefresh = () => refreshed = true;

        var notice = harness.Staging.StageResolvedFor(harness.Tab, "b.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Empty(notice);
        Assert.True(refreshed);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("Could not stage", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Contains("index.lock exists", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            harness.Statuses.Log.History,
            entry => entry.Text.StartsWith("Resolved and staged", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenAFileThatIsNotConflicted_WhenTheEditorStagesIt_ThenNothingIsStaged()
    {
        using var harness = new Harness(Modified("a.txt"));

        var notice = harness.Staging.StageResolvedFor(harness.Tab, "a.txt");
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Contains("saved, not staged", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Writer.Staged);
    }

    [Fact]
    public void GivenAConflictedFileAndNoDialog_WhenStagingIt_ThenItIsRefusedWithoutRunningGit()
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

    [Fact]
    public async Task GivenChosenLines_WhenStagingTheSelection_ThenAForwardPatchOfJustThoseLinesIsApplied()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));

        var notice = harness.Select(WorkingDiffSide.Unstaged, [new ChangedLine(DiffLineKind.Added, 2, "TWO")], true);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(string.Empty, notice);
        var patch = Assert.Single(harness.Writer.Patches);
        Assert.Equal(PatchDirection.Forward, patch.Direction);
        Assert.Equal(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1,3 +1,4 @@\n one\n two\n+TWO\n three\n",
            patch.Text);
        Assert.Equal("Staged 1 line of a.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAStagedFile_WhenUnstagingTheSelection_ThenAReversePatchIsApplied()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Staged, "a.txt", EditedFile("a.txt"));

        harness.Select(WorkingDiffSide.Staged, [new ChangedLine(DiffLineKind.Added, 4, "four")], false);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(PatchDirection.Reverse, Assert.Single(harness.Writer.Patches).Direction);
        Assert.Equal("Unstaged change (1 line) of a.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenALineThatIsNoLongerInTheFile_WhenStagingTheSelection_ThenNothingIsAppliedAndAWarningShows()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));

        harness.Select(WorkingDiffSide.Unstaged, [new ChangedLine(DiffLineKind.Added, 2, "something else")], true);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Empty(harness.Writer.Patches);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("changed since the diff was read", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenGitRefusesThePatch_WhenStagingTheSelection_ThenTheErrorIsShownAndTheStatusIsReRead()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        harness.Writer.Failure = new InvalidOperationException("patch does not apply");
        var refreshed = false;
        harness.OnRefresh = () => refreshed = true;

        harness.Select(WorkingDiffSide.Unstaged, [new ChangedLine(DiffLineKind.Added, 2, "TWO")], true);
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.True(refreshed);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("patch does not apply", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenADeletedFileWithoutHunks_WhenStagingTheSelection_ThenTheWholeFileIsStaged()
    {
        using var harness = new Harness(Deleted("new.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "new.txt");

        harness.Select(WorkingDiffSide.Unstaged, [new ChangedLine(DiffLineKind.Added, 1, "x")], false, Deleted("new.txt"));
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Equal(["new.txt"], Assert.Single(harness.Writer.Staged));
        Assert.Empty(harness.Writer.Patches);
        Assert.Equal("Staged new.txt whole: new, deleted and binary files are not split into hunks", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenLinesPickedInAFileThatCannotBeSplit_WhenStaging_ThenNothingIsStagedAndAWarningShows()
    {
        using var harness = new Harness(Deleted("new.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "new.txt");

        harness.Select(WorkingDiffSide.Unstaged, [new ChangedLine(DiffLineKind.Added, 1, "x")], true, Deleted("new.txt"));
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Empty(harness.Writer.Staged);
        Assert.Empty(harness.Writer.Patches);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("cannot be split into lines", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenASelectionInProgress_WhenAnotherArrives_ThenItIsRefusedAsBusy()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", EditedFile("a.txt"));
        harness.Writer.IsHeld = true;
        var lines = new[] { new ChangedLine(DiffLineKind.Added, 2, "TWO") };

        Assert.Equal(string.Empty, harness.Select(WorkingDiffSide.Unstaged, lines, true));
        var notice = harness.Select(WorkingDiffSide.Unstaged, lines, true);
        harness.Writer.Release();
        await harness.Pump.RunUntilAsync(() => !harness.Staging.IsRunning);

        Assert.Contains("Busy", notice, StringComparison.Ordinal);
        Assert.Single(harness.Writer.Patches);
    }

    // One hunk: "two" becomes "TWO" and a line "four" is added at the end.
    private static DiffFile EditedFile(string path)
    {
        DiffLine[] lines =
        [
            new(DiffLineKind.Context, "one", 1, 1, false),
            new(DiffLineKind.Removed, "two", 2, null, false),
            new(DiffLineKind.Added, "TWO", null, 2, false),
            new(DiffLineKind.Context, "three", 3, 3, false),
            new(DiffLineKind.Added, "four", null, 4, false),
        ];
        var hunk = new DiffHunk("@@ -1,3 +1,5 @@", 1, 3, 1, 5, string.Empty, lines);
        string[] header = [$"diff --git a/{path} b/{path}", $"--- a/{path}", $"+++ b/{path}"];
        return new DiffFile(path, path, DiffFileStatus.Modified, null, null, null, false, header, [hunk]);
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
            Staging = new StagingActions(_host, Writer, new PatchStager(Reader, Writer), Statuses, Pump.Post, RefreshStatus, _ => TestContext.Current.CancellationToken);
        }

        public UiPump Pump { get; }

        public FakeIndexWriter Writer { get; } = new();

        public FakeDiffReader Reader { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public StagingActions Staging { get; }

        /// <summary>Stands in for the tab refresher: runs when a status read is requested, before the completion callback.</summary>
        public Action OnRefresh { get; set; } = () => { };

        public RepositoryState Tab => _host.Workspace.ActiveTab!;

        public WorkingTreeSelection Selection => Tab.WorkingTree!.Selection!;

        public string Run(ActionId action) => Staging.Execute(Tab, action);

        /// <summary>Stages or unstages chosen lines of the file under the cursor (or of <paramref name="entry"/>).</summary>
        public string Select(WorkingDiffSide side, IReadOnlyList<ChangedLine> lines, bool isRange, StatusEntry? entry = null)
        {
            var shown = entry ?? (side == WorkingDiffSide.Staged ? Tab.WorkingTree!.Staged[0] : Tab.WorkingTree!.Unstaged[0]);
            return Staging.ExecuteSelection(Tab, new DiffStagingRequest(new WorkingDiffRequest(side, shown), lines, isRange));
        }

        /// <summary>Makes the next status re-read show these entries.</summary>
        public void ReloadOnRefresh(params StatusEntry[] entries) => OnRefresh = () => Load(entries);

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

        public void UseTree()
        {
            Send(new TogglePathStyleCommand());
            Send(new TogglePathStyleCommand());
        }

        public void RunRange(ActionId action, int first, int last) =>
            Staging.Execute(Tab, action, new RowRange(first, last));

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

        public void Send(RepositoryCommand command) =>
            _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, command));
    }
}
