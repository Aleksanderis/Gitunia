using System.Drawing;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Discard;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class WorkingDiffControllerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAModifiedFileUnderTheCursor_WhenOpeningTheView_ThenItsWorkingTreeDiffLoads()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        Assert.Equal("M a.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Removed, Text: "two" });
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "TWO" });
        Assert.Contains("unstaged", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheCursorMovesDownTheList_WhenItReachesAnUntrackedFile_ThenTheFileShowsAsAdditions()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        await harness.ExecuteAsync(ActionIds.ListDown);

        Assert.Equal("A new.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "brand new" });
    }

    [Fact]
    public async Task GivenTheStagedList_WhenItTakesFocus_ThenTheIndexDiffOfItsFileShows()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        await harness.ExecuteAsync(ActionIds.PaneNext);

        Assert.Equal("M b.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "staged" });
        Assert.Contains("staged", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAListFocused_WhenGoingIntoTheDiffAndBack_ThenTheViewStaysAndTheLeaveKeysStepOutOneLevelAtATime()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);

        await harness.ExecuteAsync(ActionIds.NavigateBack);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);

        await harness.ExecuteAsync(ActionIds.NavigateBack);
        Assert.Equal(TabViewMode.History, harness.Tab.View.Mode);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenMovingDown_ThenTheDiffCursorMovesAndTheListSelectionStays()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.ListDown, 2);

        Assert.Equal(2, harness.DiffList.SelectedItem);
        Assert.Equal("a.txt", harness.Tab.WorkingTree!.Selection!.Path);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenJumpingBetweenHunks_ThenTheCursorLandsOnTheHunkHeaders()
    {
        var original = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", new TestFile("long.txt", original));
        repository.WriteFile("long.txt", original.Replace("line 2\n", "two\n").Replace("line 38\n", "thirty-eight\n"));
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffNextHunk);
        await harness.ExecuteAsync(ActionIds.DiffNextHunk);

        var document = harness.Document();
        Assert.Equal(2, document.HunkRows.Count);
        Assert.Equal(document.HunkRows[1], harness.DiffList.SelectedItem);
        await harness.ExecuteAsync(ActionIds.DiffPreviousHunk);
        Assert.Equal(document.HunkRows[0], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAShownFileEditedAgainWithTheSameStatus_WhenTheStatusIsRead_ThenTheDiffReloadsAndKeepsTheCursor()
    {
        using var repository = DefaultRepository();
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        await harness.ExecuteAsync(ActionIds.ListDown, 3);
        var cursor = harness.DiffList.SelectedItem;
        repository.WriteFile("a.txt", "one\nTWO\nthree\nfour\n");

        await harness.RefreshStatusAsync();

        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "four" });
        Assert.Equal(cursor, harness.DiffList.SelectedItem);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAStatusThatChanged_WhenItIsLoaded_ThenTheDiffOfTheFileUnderTheCursorLoadsAgain()
    {
        using var repository = DefaultRepository();
        using var harness = await Harness.OpenAsync(repository);
        repository.Git("add", "a.txt");

        await harness.RefreshStatusAsync();

        Assert.DoesNotContain(harness.Tab.WorkingTree!.Unstaged, entry => entry.Path == "a.txt");
        Assert.Equal("A new.txt", harness.Rows()[harness.Document().FileRows[0]]);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenSearching_ThenTheCursorLandsOnTheMatchAndEscapeEndsTheSearch()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffSearchOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("TWO");
        harness.Controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("TWO", harness.Document().Rows[harness.CursorRow()].Text, ignoreCase: true);

        await harness.ExecuteAsync(ActionIds.NavigateBack);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
        Assert.Empty(harness.Controller.StatusLine);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenChangingOptions_ThenTheFileLoadsAgainWithThem()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffToggleWholeFile);

        Assert.True(harness.Session.WorkingDiff!.Store.Options.WholeFile);
        Assert.Equal(DiffStatus.Loaded, harness.Session.WorkingDiff.Store.Status);
        Assert.Contains("[file]", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenTogglingWrapAndSideBySide_ThenTheSourceFollows()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffToggleWrap);
        await harness.ExecuteAsync(ActionIds.DiffToggleSideBySide);

        Assert.True(harness.Session.WorkingDiff!.Source.IsWrapped);
        Assert.True(harness.Session.WorkingDiff.Source.IsSideBySide);
        Assert.Contains("[wrap]", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenCopyingTheLine_ThenTheClipboardGetsItWithItsPrefix()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        harness.DiffList.SelectedItem = harness.Document().Rows.ToList().FindIndex(row => row.Kind == DiffRowKind.Added);

        await harness.ExecuteAsync(ActionIds.DiffCopyLine);

        Assert.Contains("diff line", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnEmptyWorkingTree_WhenOpeningTheView_ThenTheDiffBoxSaysNoFileIsSelected()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        using var harness = await Harness.OpenAsync(repository);

        Assert.Equal("No file selected", Assert.Single(harness.Document().Rows).Text);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenADeletedFile_WhenSelected_ThenItsLinesShowAsRemoved()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("gone.txt", "x\ny\n"));
        repository.Delete("gone.txt");
        using var harness = await Harness.OpenAsync(repository);

        Assert.Equal("D gone.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Equal(["x", "y"], harness.Document().Rows.Where(row => row.Kind == DiffRowKind.Removed).Select(row => row.Text));
    }

    [Fact]
    public async Task GivenAStagedRename_WhenSelected_ThenTheDiffNamesBothPaths()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("old.txt", "a\nb\nc\nd\ne\nf\ng\nh\n"));
        repository.Git("mv", "old.txt", "new.txt");
        using var harness = await Harness.OpenAsync(repository);

        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
        Assert.Equal("R100 old.txt -> new.txt", harness.Rows()[harness.Document().FileRows[0]]);
    }

    [Fact]
    public async Task GivenAnUntrackedBinaryFile_WhenSelected_ThenTheDiffSaysThatItIsBinary()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "logo.bin"), [0, 1, 2, 3, 0, 255]);
        using var harness = await Harness.OpenAsync(repository);

        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Note, Text: "Binary file, content not shown" });
    }

    [Fact]
    public async Task GivenAConflictedFile_WhenSelected_ThenTheCombinedDiffShowsTheConflictMarkers()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\nc\n"));
        repository.CheckoutNewBranch("topic").Commit("topic", new TestFile("f.txt", "a\ntheirs\nc\n"));
        repository.Checkout("main").Commit("main", new TestFile("f.txt", "a\nours\nc\n"));
        repository.MergeExpectingConflict("topic");
        using var harness = await Harness.OpenAsync(repository);

        Assert.StartsWith("@@@", harness.Document().Rows.First(row => row.Kind == DiffRowKind.HunkHeader).Text, StringComparison.Ordinal);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "<<<<<<< HEAD" });
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "theirs" });
    }

    [Fact]
    public async Task GivenAFileWithAGlobInItsName_WhenSelected_ThenOnlyThatFilesDiffShows()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a*.txt", "1\n"), new TestFile("ab.txt", "1\n"));
        repository.WriteFile("a*.txt", "star\n").WriteFile("ab.txt", "b\n");
        using var harness = await Harness.OpenAsync(repository);

        Assert.Single(harness.Document().FileRows);
        Assert.Equal("M a*.txt", harness.Rows()[harness.Document().FileRows[0]]);
    }

    [Fact]
    public async Task GivenAPathThatIsNotValidUtf8_WhenSelected_ThenTheDiffBoxExplainsItCannotBeShown()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        using var harness = await Harness.OpenAsync(repository);
        var entry = new StatusEntry(
            StatusEntryKind.Untracked,
            $"bad{(char)0xDCE9}.txt",
            null,
            FileChangeKind.Unmodified,
            FileChangeKind.Unmodified,
            null,
            ConflictKind.None,
            null);

        harness.Session.WorkingDiff!.Store.Show(new WorkingDiffRequest(WorkingDiffSide.Unstaged, entry), 99);
        await harness.WaitAsync(() => harness.Session.WorkingDiff.Store.Status == DiffStatus.Failed);

        Assert.Contains("not valid UTF-8", harness.Document().Rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTwoHunks_WhenStagingInTheDiff_ThenTheHunkUnderTheCursorIsStagedAndTheCursorMovesToTheNext()
    {
        var original = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", new TestFile("long.txt", original));
        var changed = original.Replace("line 2\n", "two\n", StringComparison.Ordinal).Replace("line 38\n", "thirty-eight\n", StringComparison.Ordinal);
        repository.WriteFile("long.txt", changed);
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.StageAsync(ActionIds.WorkingTreeStage);

        Assert.Equal(original.Replace("line 2\n", "two\n", StringComparison.Ordinal), harness.Git("show", ":long.txt"));
        Assert.Equal("Staged hunk (2 lines) of long.txt", harness.Statuses.Log.Latest!.Text);
        Assert.Single(harness.Document().HunkRows);
        Assert.Equal(harness.Document().HunkRows[0], harness.CursorRow());
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAVisualRangeOverOneAddedLine_WhenStaging_ThenOnlyThatLineIsStagedAndTheRangeEnds()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB1\nB2\nc\n");
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        var addedRow = harness.Document().Rows.ToList().FindIndex(row => row is { Kind: DiffRowKind.Added, Text: "B1" });
        await harness.ExecuteAsync(ActionIds.ListDown, addedRow);
        await harness.ExecuteAsync(ActionIds.VisualToggle);
        Assert.Equal(KeyMode.Visual, harness.Mode);

        await harness.StageAsync(ActionIds.WorkingTreeStage);

        Assert.Equal("a\nb\nB1\nc\n", harness.Git("show", ":f.txt"));
        Assert.Equal("Staged 1 line of f.txt", harness.Statuses.Log.Latest!.Text);
        Assert.False(harness.Visual.IsActive);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenAVisualRangeOverContextOnly_WhenStaging_ThenItIsRefusedAndNothingChanges()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB\nc\n");
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        var contextRow = harness.Document().Rows.ToList().FindIndex(row => row is { Kind: DiffRowKind.Context, Text: "a" });
        await harness.ExecuteAsync(ActionIds.ListDown, contextRow);
        await harness.ExecuteAsync(ActionIds.VisualToggle);

        Handle(harness, ActionIds.WorkingTreeStage);

        Assert.Equal("No added or removed line is selected", harness.Handler.Message);
        Assert.Equal("a\nb\nc\n", harness.Git("show", ":f.txt"));
        Assert.True(harness.Visual.IsActive);
    }

    [Fact]
    public async Task GivenAStagedHunk_WhenUnstagingInTheStagedDiff_ThenItReturnsToTheWorkingTree()
    {
        var original = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", new TestFile("long.txt", original));
        repository.WriteFile(
            "long.txt",
            original.Replace("line 2\n", "two\n", StringComparison.Ordinal).Replace("line 38\n", "thirty-eight\n", StringComparison.Ordinal));
        repository.Git("add", "long.txt");
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeToggle);
        await harness.ExecuteAsync(ActionIds.WorkingTreeToggle);
        await harness.ExecuteAsync(ActionIds.PaneNext);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        Assert.Equal(PaneKind.WorkingStagedDiff, harness.Tab.View.FocusedPane);

        await harness.StageAsync(ActionIds.WorkingTreeUnstage);

        Assert.Equal(original.Replace("line 38\n", "thirty-eight\n", StringComparison.Ordinal), harness.Git("show", ":long.txt"));
        Assert.Equal("Unstaged hunk (2 lines) of long.txt", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAModifiedBinaryFile_WhenStagingInTheDiff_ThenTheWholeFileIsStagedAndSaysSo()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "a\n"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "pic.bin"), [0, 1, 2, 0, 255]);
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.StageAsync(ActionIds.WorkingTreeStage);

        Assert.Equal("Staged pic.bin whole: new, deleted and binary files are not split into hunks", harness.Statuses.Log.Latest!.Text);
        Assert.Equal("A  pic.bin\n", harness.Git("status", "--short"));
    }

    [Fact]
    public async Task GivenTheUnstagedDiff_WhenUnstaging_ThenItIsRefusedWithAHint()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        Handle(harness, ActionIds.WorkingTreeUnstage);

        Assert.Equal("Unstage works in the Staged diff", harness.Handler.Message);
    }

    [Fact]
    public async Task GivenAnActiveRange_WhenQuitting_ThenTheRangeEndsFirst()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        await harness.ExecuteAsync(ActionIds.VisualToggle);

        Assert.True(harness.Handler.TryExecute(ActionIds.AppQuit, null));

        Assert.False(harness.Visual.IsActive);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenAConflictedFile_WhenStagingInTheDiff_ThenItIsRefused()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\n"));
        repository.CheckoutNewBranch("topic").WriteFile("f.txt", "a\ntopic\n");
        repository.Git("commit", "-q", "-am", "topic");
        repository.Checkout("main").WriteFile("f.txt", "a\nmain\n");
        repository.Git("commit", "-q", "-am", "main");
        repository.MergeExpectingConflict("topic");
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        Handle(harness, ActionIds.WorkingTreeStage);

        Assert.Contains("is conflicted", harness.Handler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTwoHunks_WhenDiscardingTheFirstAndTheDialogIsDropped_ThenNothingChanges()
    {
        var original = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", new TestFile("long.txt", original));
        var changed = original.Replace("line 2\n", "two\n", StringComparison.Ordinal).Replace("line 38\n", "thirty-eight\n", StringComparison.Ordinal);
        repository.WriteFile("long.txt", changed);
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        Handle(harness, ActionIds.WorkingTreeDiscard);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal(["Discard this hunk (2 lines) of long.txt?"], prompt.Lines);
        harness.DropDialog();
        Assert.Equal(changed, File.ReadAllText(Path.Combine(repository.WorkingTreePath, "long.txt")));
        Assert.Null(harness.Statuses.Log.Latest);
    }

    [Fact]
    public async Task GivenTwoHunks_WhenDiscardingTheFirstAndConfirming_ThenOnlyTheSecondEditRemainsAndTheIndexIsUntouched()
    {
        var original = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", new TestFile("long.txt", original));
        var changed = original.Replace("line 2\n", "two\n", StringComparison.Ordinal).Replace("line 38\n", "thirty-eight\n", StringComparison.Ordinal);
        repository.WriteFile("long.txt", changed);
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        Handle(harness, ActionIds.WorkingTreeDiscard);
        await harness.ConfirmAsync();

        Assert.Equal(original.Replace("line 38\n", "thirty-eight\n", StringComparison.Ordinal), File.ReadAllText(Path.Combine(repository.WorkingTreePath, "long.txt")));
        Assert.Equal(original, harness.Git("show", ":long.txt"));
        Assert.Equal("Discarded hunk (2 lines) of long.txt", harness.Statuses.Log.Latest!.Text);
        Assert.Single(harness.Document().HunkRows);
    }

    [Fact]
    public async Task GivenAVisualRangeOverOneAddedLine_WhenDiscarding_ThenTheRangeSurvivesTheDialogAndEndsOnYes()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB1\nB2\nc\n");
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        var addedRow = harness.Document().Rows.ToList().FindIndex(row => row is { Kind: DiffRowKind.Added, Text: "B1" });
        await harness.ExecuteAsync(ActionIds.ListDown, addedRow);
        await harness.ExecuteAsync(ActionIds.VisualToggle);

        Handle(harness, ActionIds.WorkingTreeDiscard);
        Assert.Equal(["Discard the 1 selected line of f.txt?"], Assert.Single(harness.Prompts).Lines);
        Assert.True(harness.Visual.IsActive);
        await harness.ConfirmAsync();

        Assert.Equal("a\nB2\nc\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "f.txt")));
        Assert.Equal("Discarded 1 line of f.txt", harness.Statuses.Log.Latest!.Text);
        Assert.False(harness.Visual.IsActive);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenAModifiedFileInTheList_WhenDiscardingAndConfirming_ThenItIsRestoredAndItsStagedChangesStay()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "one\n"));
        repository.WriteFile("a.txt", "staged\n");
        repository.Git("add", "a.txt");
        repository.WriteFile("a.txt", "staged and more\n");
        using var harness = await Harness.OpenAsync(repository);

        Handle(harness, ActionIds.WorkingTreeDiscard);
        Assert.Contains("Its staged changes are kept.", Assert.Single(harness.Prompts).Lines);
        await harness.ConfirmAsync();

        Assert.Equal("staged\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
        Assert.Equal("staged\n", harness.Git("show", ":a.txt"));
    }

    [Fact]
    public async Task GivenAnUntrackedFileInTheList_WhenDiscardingAndConfirming_ThenItIsDeletedAndTrackedFilesStay()
    {
        using var repository = DefaultRepository();
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.ListDown);

        Handle(harness, ActionIds.WorkingTreeDiscard);
        Assert.Equal("Delete untracked file", Assert.Single(harness.Prompts).Title);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
        await harness.ConfirmAsync();

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
        Assert.Equal("one\nTWO\nthree\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenAnUntrackedFileInTheList_WhenDiscardingAndDroppingTheDialog_ThenTheFileStays()
    {
        using var repository = DefaultRepository();
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.ListDown);

        Handle(harness, ActionIds.WorkingTreeDiscard);
        harness.DropDialog();

        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
    }

    [Fact]
    public async Task GivenTheStagedDiff_WhenDiscarding_ThenItIsRefusedWithAHint()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.PaneNext);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        Handle(harness, ActionIds.WorkingTreeDiscard);

        Assert.Contains("unstage", harness.Handler.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Prompts);
    }

    private static void Handle(Harness harness, ActionId action) => harness.Handler.TryExecute(action, null);

    private static TestRepository DefaultRepository()
    {
        var repository = TestRepository.Create().Commit(
            "base",
            new TestFile("a.txt", "one\ntwo\nthree\n"),
            new TestFile("b.txt", "b\n"));
        repository.WriteFile("a.txt", "one\nTWO\nthree\n");
        repository.WriteFile("new.txt", "brand new\n");
        repository.WriteFile("b.txt", "b\nstaged\n");
        repository.Git("add", "b.txt");
        return repository;
    }

    private sealed class Harness : IDisposable
    {
        private readonly UiPump _pump;
        private readonly TestRepository _repository;
        private readonly List<Action> _pendingAnswers = [];
        private KeyMode _mode = KeyMode.Normal;

        private Harness(TestRepository repository, WorkspaceHost host, TabSession session, UiPump pump)
        {
            _repository = repository;
            _pump = pump;
            Host = host;
            Session = session;
            Statuses = new StatusChannel();
            DiffList = new ListView { X = 0, Y = 0, Width = 60, Height = 20 };
            DiffList.SetRelativeLayout(new Size(80, 24));
            var copier = new ClipboardCopier(new ClipboardWriter(new StringWriter(), new Osc52Options(Osc52Mode.Direct, 100_000)), Statuses);
            Controller = new WorkingDiffController(host, () => Session, DiffList, mode => _mode = mode, () => { }, copier);
            Controller.OnTabSwitched(null, session);
            var indexWriter = TestGit.Services.IndexWriter;
            Staging = new StagingActions(
                host,
                indexWriter,
                new PatchStager(TestGit.Services.Diff, indexWriter),
                Statuses,
                pump.Post,
                (_, done) =>
                {
                    pump.Post(done);
                    return true;
                },
                _ => Token,
                Gate);
            Visual = new VisualRangeController(() => Tab.View.FocusedPane, _ => DiffList, () => _mode, mode => _mode = mode);
            Handler = new WorkingTreeActionHandler(host, () => 10, copier, Staging, TestCommitActions.Create(host, Statuses)) { Diff = Controller, Visual = Visual };
            Handler.Discard = new DiscardActions(
                host,
                new PatchDiscarder(TestGit.Services.Diff, TestGit.Services.Discarder),
                Statuses,
                pump.Post,
                (_, done) =>
                {
                    pump.Post(done);
                    return true;
                },
                _ => Token,
                AskConfirm,
                Gate);
        }

        public StagingActions Staging { get; }

        public TabOperationGate Gate { get; } = new();

        /// <summary>The confirm dialogs the actions asked for, oldest first.</summary>
        public List<ConfirmPrompt> Prompts { get; } = [];

        public VisualRangeController Visual { get; }

        public WorkspaceHost Host { get; }

        public TabSession Session { get; }

        public StatusChannel Statuses { get; }

        public ListView DiffList { get; }

        public WorkingDiffController Controller { get; }

        public WorkingTreeActionHandler Handler { get; }

        public KeyMode Mode => _mode;

        public RepositoryState Tab => Host.Workspace.ActiveTab!;

        public static async Task<Harness> OpenAsync(TestRepository repository)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pump = new UiPump(Token);
            var session = new TabSession(
                host.Workspace.ActiveTabId!.Value,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, pump.Post, TimeSpan.Zero),
                workingDiffStore: new WorkingDiffStore(TestGit.Services.Diff, location, pump.Post));
            var harness = new Harness(repository, host, session, pump);
            await harness.LoadStatusAsync();
            harness.Handler.TryExecute(ActionIds.WorkingTreeOpen, null);
            await harness.WaitForLoadedAsync();
            return harness;
        }

        public DiffDocument Document() => Session.WorkingDiff!.Store.Document;

        public IReadOnlyList<string> Rows() => [.. Document().Rows.Select(row => row.Text)];

        public int CursorRow() => Session.WorkingDiff!.Source.RowOf(DiffList.SelectedItem ?? 0);

        public async Task ExecuteAsync(ActionId action, int? count = null)
        {
            Handler.TryExecute(action, count);
            await WaitForLoadedAsync();
        }

        /// <summary>Runs a stage or unstage action, waits for git, then re-reads the status like the tab refresher does.</summary>
        public async Task StageAsync(ActionId action)
        {
            Handler.TryExecute(action, null);
            await _pump.RunUntilAsync(() => !Staging.IsRunning);
            await RefreshStatusAsync();
        }

        /// <summary>Answers <c>y</c> in the open dialog, waits for the discard, then re-reads the status like the tab refresher does.</summary>
        public async Task ConfirmAsync()
        {
            var answer = Assert.Single(_pendingAnswers);
            _pendingAnswers.Clear();
            answer();
            await _pump.RunUntilAsync(() => !Gate.IsAnyBusy);
            await RefreshStatusAsync();
        }

        /// <summary>Answers <c>n</c> or <c>Esc</c>: the callback is dropped, as the overlay does.</summary>
        public void DropDialog() => _pendingAnswers.Clear();

        public string Git(params string[] arguments) => _repository.Git(arguments);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString()));
            }
        }

        public async Task RefreshStatusAsync()
        {
            var snapshot = await ReadStatusAsync();
            Host.Dispatch(new TabCommand(Tab.Id, new WorkingTreeLoadedCommand(snapshot)));
            if (Session.WorkingDiff!.Store.Status != DiffStatus.Loading)
            {
                // The letters did not change: the refresher tells the session so the shown file is read again.
                Session.NotifyStatusRead();
            }

            await WaitForLoadedAsync();
        }

        public Task WaitAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose()
        {
            Session.Dispose();
            _repository.Dispose();
        }

        private bool AskConfirm(ConfirmPrompt prompt, Action onYes)
        {
            Prompts.Add(prompt);
            _pendingAnswers.Add(onYes);
            return true;
        }

        private async Task LoadStatusAsync()
        {
            var snapshot = await ReadStatusAsync();
            Host.Dispatch(new TabCommand(Tab.Id, new WorkingTreeLoadedCommand(snapshot)));
        }

        private async Task<StatusSnapshot> ReadStatusAsync() =>
            await TestGit.Services.Status.ReadStatusAsync(Tab.Location, false, UntrackedFilesMode.All, Token);

        private async Task WaitForLoadedAsync()
        {
            var store = Session.WorkingDiff!.Store;
            await _pump.RunUntilAsync(() => store.Status != DiffStatus.Loading);
        }
    }
}
