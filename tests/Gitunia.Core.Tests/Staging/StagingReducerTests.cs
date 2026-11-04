using Gitunia.Core.Effects;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Staging;

// The pure part of staging: what a key touches, the refusals and the reports. Real git is the handler's tests.
[Trait("Os", "Independent")]
public class StagingReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RepositoryLocation Location = new("/r", "/r/.git", "/r/.git", false, false);

    [Fact]
    public void GivenAFileUnderTheCursor_WhenStaging_ThenOnlyItsPathIsStagedAndNamed()
    {
        var tree = Select(Tree(Modified("a.txt"), Modified("b.txt"), Modified("c.txt")), WorkingTreeSection.Unstaged, 1);

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(tree));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["b.txt"], effect.Plan.Paths);
        Assert.Equal("b.txt", effect.Plan.Name);
        Assert.True(effect.IsStage);
        Assert.Equal(StagingFollow.None, effect.Follow);
        Assert.Equal("Staged b.txt", StagingReports.Paths(effect.Plan, true));
    }

    [Fact]
    public void GivenTheTreeStyleAndAFolderRow_WhenStaging_ThenEveryFileUnderItIsStagedAndOthersAreNot()
    {
        var tree = Select(
            TreeOf(FilePathStyle.Tree, Modified("src/a.cs"), Modified("src/deep/b.cs"), Modified("top.txt")),
            WorkingTreeSection.Unstaged,
            0);
        Assert.True(tree.Selection!.IsFolder);

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(tree));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["src/a.cs", "src/deep/b.cs"], effect.Plan.Paths);
        Assert.Equal("Staged src/ (2 files)", StagingReports.Paths(effect.Plan, true));
    }

    [Fact]
    public void GivenAClosedFolderInTheTreeStyle_WhenStagingIt_ThenTheHiddenFilesAreStaged()
    {
        var tree = TreeOf(FilePathStyle.Tree, Modified("src/a.cs"), Modified("src/b.cs"));
        tree = tree.WithLayout(FilePathStyle.Tree, ["src"], []);
        tree = Select(tree, WorkingTreeSection.Unstaged, 0);
        Assert.Equal(1, tree.RowsOf(WorkingTreeSection.Unstaged).Count);

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(tree));

        Assert.Equal(["src/a.cs", "src/b.cs"], Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects)).Plan.Paths);
    }

    [Fact]
    public void GivenATreeRangeFromAFolderRowToItsFirstFile_WhenStaging_ThenEachFileIsStagedOnce()
    {
        var tree = TreeOf(FilePathStyle.Tree, Modified("src/a.cs"), Modified("src/b.cs"), Modified("z.txt"));

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage, new RowRange(0, 1)), Context(tree));

        Assert.Equal(["src/a.cs", "src/b.cs"], Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects)).Plan.Paths);
    }

    [Fact]
    public void GivenTheTreeStyleAndAFileRow_WhenStaging_ThenTheFileOfThatRowNotTheRowNumberIsStaged()
    {
        var tree = Select(
            TreeOf(FilePathStyle.Tree, Modified("a/x.cs"), Modified("b/y.cs"), Modified("c.txt")),
            WorkingTreeSection.Unstaged,
            3);
        Assert.Equal("b/y.cs", tree.Selection!.Path);

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(tree));

        Assert.Equal(["b/y.cs"], Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects)).Plan.Paths);
    }

    [Fact]
    public void GivenARangeOverThreeFiles_WhenStaging_ThenAllOfThemGoInOneEffect()
    {
        var tree = Tree(Modified("a.txt"), Modified("b.txt"), Modified("c.txt"), Modified("d.txt"));

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage, new RowRange(1, 3)), Context(tree));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["b.txt", "c.txt", "d.txt"], effect.Plan.Paths);
        Assert.Equal("Staged 3 files", StagingReports.Paths(effect.Plan, true));
    }

    [Fact]
    public void GivenARangeWithAConflictedFile_WhenStaging_ThenItIsSkippedAndCounted()
    {
        var tree = Tree(Modified("a.txt"), Conflict("c.txt"), Modified("d.txt"));

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage, new RowRange(0, 2)), Context(tree));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["a.txt", "d.txt"], effect.Plan.Paths);
        Assert.Equal("Staged 2 files (1 conflicted skipped)", StagingReports.Paths(effect.Plan, true));
    }

    [Fact]
    public void GivenARangeOfOnlyConflictedFiles_WhenStaging_ThenItIsRefusedAndNothingRuns()
    {
        var tree = Tree(Conflict("c.txt"), Conflict("d.txt"));

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage, new RowRange(0, 1)), Context(tree));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("Only conflicted files are selected (2 skipped; mark one resolved on its own)", post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenAStagedRename_WhenUnstaging_ThenBothPathsAreSent()
    {
        var tree = Tree(StagedRename("old.txt", "new.txt"));

        var update = Reduce(new StagingRequested(Tab, StagingAction.Unstage), Context(tree, PaneKind.WorkingStaged));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["new.txt", "old.txt"], effect.Plan.Paths);
        Assert.False(effect.IsStage);
        Assert.False(effect.IsUnborn);
    }

    [Fact]
    public void GivenAnUnbornBranch_WhenUnstaging_ThenTheEffectSaysThereIsNoHead()
    {
        var snapshot = Snapshot(StagedAdded("a.txt"));
        var branch = snapshot.Branch with { Head = new HeadState(HeadKind.Unborn, null, null) };
        var tree = WorkingTreeState.Reconcile(null, snapshot with { Branch = branch });

        var update = Reduce(new StagingRequested(Tab, StagingAction.Unstage), Context(tree, PaneKind.WorkingStaged));

        Assert.True(Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects)).IsUnborn);
    }

    [Fact]
    public void GivenConflictedFiles_WhenStagingAll_ThenTheyAreSkippedAndCounted()
    {
        var tree = Tree(Conflict("c.txt"), Modified("a.txt"), Untracked("n.txt"));

        var update = Reduce(new StagingRequested(Tab, StagingAction.StageAll), Context(tree));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["a.txt", "n.txt"], effect.Plan.Paths);
        Assert.Equal("Staged 2 files (1 conflicted skipped)", StagingReports.Paths(effect.Plan, true));
    }

    [Fact]
    public void GivenOnlyConflictedFilesLeft_WhenStagingAll_ThenItSaysSo()
    {
        var update = Reduce(new StagingRequested(Tab, StagingAction.StageAll), Context(Tree(Conflict("c.txt"))));

        Assert.Equal("Only conflicted files are left (1 skipped)", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenNothingToUnstage_WhenUnstagingAll_ThenNothingRuns()
    {
        var update = Reduce(
            new StagingRequested(Tab, StagingAction.UnstageAll),
            Context(Tree(Modified("a.txt")), PaneKind.WorkingStaged));

        Assert.Equal("Nothing to do", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheStagedList_WhenStaging_ThenItSaysWhereStagingWorks()
    {
        var update = Reduce(
            new StagingRequested(Tab, StagingAction.Stage),
            Context(Tree(StagedModified("a.txt")), PaneKind.WorkingStaged));

        Assert.Equal("Stage works in the Unstaged list", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheUnstagedList_WhenUnstaging_ThenItSaysWhereUnstagingWorks()
    {
        var update = Reduce(new StagingRequested(Tab, StagingAction.Unstage), Context(Tree(Modified("a.txt"))));

        Assert.Equal("Unstage works in the Staged list", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheGraphHasTheFocus_WhenStaging_ThenItAsksForAFileList()
    {
        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(Tree(Modified("a.txt")), PaneKind.Graph));

        Assert.Equal("Focus a file list first", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenNoStatusYet_WhenStaging_ThenItSaysTheStatusIsNotLoaded()
    {
        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(null));

        Assert.Equal("The status is not loaded yet", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenAStageThatRuns_WhenAnotherKeyArrives_ThenItIsRefusedAsBusy()
    {
        var update = Reduce(
            new StagingRequested(Tab, StagingAction.Stage),
            Context(Tree(Modified("a.txt"), Modified("b.txt")), isBusy: true));

        Assert.Equal(StagingReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenAnotherOperationWritingTheTab_WhenStaging_ThenItIsRefused()
    {
        var update = Reduce(
            new StagingRequested(Tab, StagingAction.Stage),
            Context(Tree(Modified("a.txt")), isBlocked: true));

        Assert.Equal(StagingReducer.BlockedNotice, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenAConflictedFileUnderTheCursor_WhenStaging_ThenADialogAsksAndNothingRunsYet()
    {
        var tree = Select(Tree(Conflict("c.txt"), Modified("a.txt")), WorkingTreeSection.Unstaged, 0);

        var update = Reduce(new StagingRequested(Tab, StagingAction.Stage), Context(tree));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Mark c.txt as resolved?", confirm.Prompt.Lines[0]);
        Assert.Contains("markers", confirm.Prompt.Lines[1], StringComparison.Ordinal);
        Assert.Contains("re-create the conflict from the conflict editor", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.DoesNotContain("git ", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("The screen is too small to ask: nothing was marked resolved", confirm.UnavailableNotice);
        Assert.Equal(Conflict("c.txt"), Assert.IsType<ResolveConfirmed>(confirm.OnAccepted).Entry);
    }

    [Fact]
    public void GivenTheResolveDialogIsAnswered_WhenYes_ThenTheFileIsStagedAndTheReportSaysMarkedResolved()
    {
        var tree = Tree(Conflict("c.txt"));

        var update = Reduce(new ResolveConfirmed(Tab, Conflict("c.txt")), Context(tree));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["c.txt"], effect.Plan.Paths);
        Assert.Equal("Marked c.txt resolved", StagingReports.Paths(effect.Plan, true));
    }

    [Fact]
    public void GivenTheResolveDialogIsAnswered_WhenAStageRunsMeanwhile_ThenNothingIsMarkedResolved()
    {
        var update = Reduce(new ResolveConfirmed(Tab, Conflict("c.txt")), Context(Tree(Conflict("c.txt")), isBusy: true));

        Assert.Equal(
            $"{StagingReducer.BusyNotice}; nothing was marked resolved",
            Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenNothingStaged_WhenStagingAllBeforeACommit_ThenTheCommandThatFollowsTravelsWithTheEffect()
    {
        var then = new StagingCancelled(Tab);
        var tree = Tree(Modified("a.txt"), Conflict("c.txt"));

        var update = Reduce(new StageAllThenRequested(Tab, then), Context(tree, PaneKind.Graph));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["a.txt"], effect.Plan.Paths);
        Assert.Same(then, effect.Follow.Then);
    }

    [Fact]
    public void GivenOnlyConflictedFiles_WhenStagingAllBeforeACommit_ThenItIsRefused()
    {
        var update = Reduce(new StageAllThenRequested(Tab, new StagingCancelled(Tab)), Context(Tree(Conflict("c.txt"))));

        Assert.Equal("Only conflicted files are left (1 skipped)", Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheEditorSavedAConflictedFile_WhenStagingIt_ThenItIsStagedWithoutADialogAndRemembered()
    {
        var update = Reduce(new StageResolvedRequested(Tab, "c.txt"), Context(Tree(Conflict("c.txt"))));

        var effect = Assert.IsType<StagePathsEffect>(Assert.Single(update.Effects));
        Assert.Equal(["c.txt"], effect.Plan.Paths);
        Assert.Equal(new ResolvedFile("c.txt", "c.txt"), effect.Follow.Resolved);
    }

    [Fact]
    public void GivenAFileThatIsNotConflicted_WhenTheEditorStagesIt_ThenNothingIsStagedAndTheTabIsRead()
    {
        var update = Reduce(new StageResolvedRequested(Tab, "a.txt"), Context(Tree(Modified("a.txt"))));

        Assert.Contains("saved, not staged", Assert.IsType<PostStatusEffect>(update.Effects[0]).Text, StringComparison.Ordinal);
        Assert.Equal(new RefreshTabEffect(Tab), update.Effects[1]);
    }

    [Fact]
    public void GivenAStageThatRuns_WhenTheEditorStagesTheSavedFile_ThenItIsRefusedAndTheTabIsRead()
    {
        var update = Reduce(new StageResolvedRequested(Tab, "c.txt"), Context(Tree(Conflict("c.txt")), isBusy: true));

        Assert.Equal(
            $"{StagingReducer.BusyNotice}; the saved file was not staged",
            Assert.IsType<PostStatusEffect>(update.Effects[0]).Text);
        Assert.Equal(new RefreshTabEffect(Tab), update.Effects[1]);
    }

    [Fact]
    public void GivenTheSelectionOfADiff_WhenStaging_ThenTheEffectCarriesWhatWasOnScreen()
    {
        var selection = new StagingSelection(
            WorkingDiffSide.Unstaged,
            Modified("a.txt"),
            [new ChangedLine(DiffLineKind.Added, 2, "TWO")],
            true);
        var move = new DiffCursorMove(1, 3);

        var update = Reduce(new StageSelectionRequested(Tab, selection, move), Context(Tree(Modified("a.txt"))));

        var effect = Assert.IsType<StageSelectionEffect>(Assert.Single(update.Effects));
        Assert.Same(selection, effect.Selection);
        Assert.Equal(move, effect.Move);
    }

    [Fact]
    public void GivenASelectionThatRuns_WhenAnotherArrives_ThenItIsRefusedAsBusy()
    {
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, Modified("a.txt"), [], false);

        var update = Reduce(
            new StageSelectionRequested(Tab, selection, new DiffCursorMove(0, -1)),
            Context(Tree(Modified("a.txt")), isBusy: true));

        Assert.Equal(StagingReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenASuccess_WhenReduced_ThenItsTextIsPostedWithItsSeverity()
    {
        var update = Reduce(
            new StagingSucceeded(Tab, "Staged b.txt", StatusSeverity.Info, StagingFollow.None),
            Context(Tree(Modified("a.txt"))));

        Assert.Equal(new PostStatusEffect("Staged b.txt", StatusSeverity.Info), Assert.Single(update.Effects));
        Assert.Empty(update.Next);
    }

    [Fact]
    public void GivenAFailure_WhenReduced_ThenTheErrorIsPostedAndNothingFollows()
    {
        var update = Reduce(new StagingFailed(Tab, "Could not stage: index.lock exists"), Context(Tree(Modified("a.txt"))));

        Assert.Equal(
            new PostStatusEffect("Could not stage: index.lock exists", StatusSeverity.Error),
            Assert.Single(update.Effects));
        Assert.Empty(update.Next);
    }

    [Fact]
    public void GivenACommitWaitsForStageAll_WhenItSucceeds_ThenThatCommandIsReducedNext()
    {
        var then = new StagingCancelled(Tab);

        var update = Reduce(
            new StagingSucceeded(Tab, "Staged 2 files", StatusSeverity.Info, new StagingFollow(Then: then)),
            Context(Tree(StagedModified("a.txt"))));

        Assert.Same(then, Assert.Single(update.Next));
    }

    [Fact]
    public void GivenAStagedHunk_WhenItSucceeds_ThenTheDiffCursorIsMoved()
    {
        var move = new DiffCursorMove(1, -1);

        var update = Reduce(
            new StagingSucceeded(Tab, "Staged change (2 lines) of a.txt", StatusSeverity.Info, new StagingFollow(Move: move)),
            Context(Tree(Modified("a.txt"))));

        Assert.Equal(new MoveDiffCursorEffect(Tab, move), update.Effects[1]);
    }

    [Fact]
    public void GivenThreeConflictsAndOneResolved_WhenTheStatusIsRead_ThenTwoAreReportedLeftAndTheCursorGoesToTheFirst()
    {
        var tree = Select(Tree(Conflict("a.txt"), Conflict("b.txt"), StagedModified("c.txt")), WorkingTreeSection.Unstaged, 1);

        var update = Reduce(Resolved("c.txt"), Context(tree));

        Assert.Equal(
            "Resolved and staged c.txt (Staged list), 2 conflicts left",
            Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
        var select = Assert.IsType<TabCommand>(Assert.Single(update.Next));
        Assert.Equal(new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 0), select.Command);
    }

    [Fact]
    public void GivenTheLastConflictResolved_WhenTheStatusIsRead_ThenTheStatusSaysCommitAndTheStagedFileIsFocused()
    {
        var tree = Tree(Modified("m.txt"), StagedModified("c.txt"));

        var update = Reduce(Resolved("c.txt"), Context(tree));

        Assert.Equal(
            "Resolved and staged c.txt (Staged list): all conflicts resolved, commit it",
            Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
        Assert.Equal(
            [
                new TabCommand(Tab, new SelectWorkingTreeFileCommand(WorkingTreeSection.Staged, 0)),
                new TabCommand(Tab, new FocusPaneCommand(PaneKind.WorkingStaged)),
            ],
            update.Next);
    }

    [Fact]
    public void GivenTheLastConflictResolvedDuringAnOperation_WhenTheStatusIsRead_ThenTheStatusHintsAtContinuing()
    {
        var update = Reduce(Resolved("c.txt"), Context(Tree(StagedModified("c.txt")), hasOperation: true));

        Assert.Equal(
            "Resolved and staged c.txt (Staged list): all conflicts resolved, <space>gc continue",
            Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenOneConflictLeft_WhenTheStatusIsRead_ThenTheReportIsSingular()
    {
        var update = Reduce(Resolved("c.txt"), Context(Tree(Conflict("a.txt"), StagedModified("c.txt"))));

        Assert.Equal(
            "Resolved and staged c.txt (Staged list), 1 conflict left",
            Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenACancelledStage_WhenReduced_ThenNothingIsPosted()
    {
        var update = Reduce(new StagingCancelled(Tab), Context(Tree(Modified("a.txt"))));

        Assert.Empty(update.Effects);
        Assert.Empty(update.Next);
    }

    private static StagingSucceeded Resolved(string path) =>
        new(
            Tab,
            $"Resolved and staged {path}",
            StatusSeverity.Info,
            new StagingFollow(Resolved: new ResolvedFile(path, path)));

    private static StagingUpdate Reduce(StagingCommand command, StagingContext context) =>
        StagingReducer.Reduce(command, context);

    private static StagingContext Context(
        WorkingTreeState? tree,
        PaneKind focus = PaneKind.WorkingUnstaged,
        bool isBusy = false,
        bool isBlocked = false,
        bool hasOperation = false) =>
        new(Tab, Location, focus, tree, hasOperation, isBusy, isBlocked);

    private static WorkingTreeState TreeOf(FilePathStyle style, params StatusEntry[] entries) =>
        WorkingTreeState.Reconcile(null, Snapshot(entries), style);

    private static WorkingTreeState Select(WorkingTreeState tree, WorkingTreeSection section, int row) =>
        tree with { Selection = tree.SelectionAt(section, row) };
}
