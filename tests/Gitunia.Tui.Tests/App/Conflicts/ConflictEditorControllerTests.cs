using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Conflicts;

// Real git on temp repositories through the controller, the real reader and writer.
public class ConflictEditorControllerTests
{
    private const string ConflictPath = "conflict.txt";
    private const string TwoHunkBase = "l1\nl2\nl3\nl4\nl5\nl6\nl7\nl8\nl9\nl10\nl11\nl12\n";

    public static TheoryData<string> Operations => ["merge", "rebase", "cherry-pick"];

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task GivenAStoppedOperation_WhenPickingBothAndSaving_ThenTheFileIsResolvedAndTheOperationContinues(
        string operation)
    {
        using var repository = Stopped(operation);
        using var harness = await Harness.CreateAsync(repository);

        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorBoth);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitAsync();

        Assert.False(harness.Controller.IsOpen);
        Assert.Equal("main\nother\n", File.ReadAllText(harness.FullPath));
        Assert.Equal([ConflictPath], harness.ResolvedAsked);
        Assert.Empty(harness.Prompts);
        Assert.Contains("every conflict resolved", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.Contains(ConflictPath, repository.Git("ls-files", "-u"), StringComparison.Ordinal);
        Finish(repository, operation);
        Assert.Equal("main\nother\n", repository.Git("show", "HEAD:conflict.txt").Replace("\r", string.Empty));
    }

    [Fact]
    public async Task GivenAPickOnOneHunkOfTwo_WhenTakingAllTheirsAndSaving_ThenEveryHunkIsResolvedWithoutAConfirm()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorAllTheirs);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitAsync();

        Assert.DoesNotContain("<<<<<<<", File.ReadAllText(harness.FullPath), StringComparison.Ordinal);
        Assert.Equal([ConflictPath], harness.ResolvedAsked);
    }

    [Fact]
    public async Task GivenPicksOnOneHunkOfTwo_WhenSavingAndConfirming_ThenTheOtherHunkKeepsItsMarkers()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorTheirs);
        harness.Run(ActionIds.ConflictEditorSave);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("1 unresolved conflict out of 2", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("git checkout --merge", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(harness.OriginalText, File.ReadAllText(harness.FullPath));
        harness.Answer();
        await harness.WaitAsync();

        var saved = File.ReadAllText(harness.FullPath).Replace("\r", string.Empty);
        Assert.Contains("two\n", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("II\n", saved, StringComparison.Ordinal);
        Assert.Contains("<<<<<<<", saved, StringComparison.Ordinal);
        Assert.Contains("XI\n", saved, StringComparison.Ordinal);
        Assert.Empty(harness.ResolvedAsked);
        Assert.Contains("1 conflict still unresolved", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUnresolvedHunks_WhenTheSaveConfirmIsDeclined_ThenNothingIsWrittenAndThePicksStay()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorSave);

        harness.Decline();

        Assert.Equal(harness.OriginalText, File.ReadAllText(harness.FullPath));
        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(1, harness.Controller.State!.ResolvedCount);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenALongFileWithFiveConflicts_WhenWalkingAndPicking_ThenTheCursorAndPicksFollowEachHunk()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        Assert.Equal(5, harness.Controller.State!.Hunks.Count);

        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorNext);
        harness.Run(ActionIds.ConflictEditorNext);
        harness.Run(ActionIds.ConflictEditorTheirs);
        harness.Run(ActionIds.ConflictEditorPrevious);

        var state = harness.Controller.State!;
        Assert.Equal(1, state.Index);
        Assert.Equal(
            [ConflictPick.Ours, ConflictPick.None, ConflictPick.Theirs, ConflictPick.None, ConflictPick.None],
            state.Result.Regions.Select(region => region.Pick));
        Assert.Equal(2, state.ResolvedCount);
        Assert.Equal(3, state.UnresolvedCount);
    }

    [Fact]
    public async Task GivenNothingPicked_WhenSaving_ThenItIsRefusedWithoutAPrompt()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorSave);

        Assert.Empty(harness.Prompts);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Equal(harness.OriginalText, File.ReadAllText(harness.FullPath));
    }

    [Fact]
    public async Task GivenTheFileChangedOnDiskAfterOpening_WhenSaving_ThenItIsNotOverwritten()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        File.WriteAllText(harness.FullPath, "edited elsewhere\n");

        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitAsync();

        Assert.Equal("edited elsewhere\n", File.ReadAllText(harness.FullPath));
        Assert.True(harness.Controller.IsOpen);
        Assert.Contains("changed on disk", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Empty(harness.ResolvedAsked);
    }

    [Fact]
    public async Task GivenBothSidesAddedTheFile_WhenOpeningPickingAndSaving_ThenItIsResolvedAgainstAnEmptyBase()
    {
        using var repository = AddedOnBothSides();
        using var harness = await Harness.CreateAsync(repository);

        await harness.OpenAsync();

        Assert.Equal(string.Empty, harness.Controller.State!.Selected!.Base);
        Assert.Contains("both sides added it", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        harness.Run(ActionIds.ConflictEditorTheirs);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitForStatusAsync("Saved");
        Assert.False(harness.Controller.IsOpen);
        Assert.Equal("other\n", File.ReadAllText(harness.FullPath).Replace("\r", string.Empty));
        Assert.Equal([ConflictPath], harness.ResolvedAsked);
    }

    [Fact]
    public async Task GivenBothSidesAddedTheFile_WhenEditingTheHunk_ThenTheTypedTextIsSaved()
    {
        using var repository = AddedOnBothSides();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorTheirs);
        harness.Run(ActionIds.ConflictEditorEdit);
        harness.Type("!");
        harness.Run(ActionIds.ConflictEditorEditDone);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitForStatusAsync("Saved");

        Assert.DoesNotContain("<<<<<<<", File.ReadAllText(harness.FullPath), StringComparison.Ordinal);
        Assert.Equal("!other\n", File.ReadAllText(harness.FullPath).Replace("\r", string.Empty));
    }

    [Fact]
    public async Task GivenOneSideDeletedTheFile_WhenOpening_ThenTheEditorStaysClosedAndNamesTheMergetool()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile(ConflictPath, "x\n"));
        repository.CheckoutNewBranch("other").Delete(ConflictPath);
        repository.Git("commit", "-a", "-m", "Delete");
        repository.Checkout("main").Commit("Change", new TestFile(ConflictPath, "main\n"))
            .MergeExpectingConflict("other");
        using var harness = await Harness.CreateAsync(repository);

        Assert.True(harness.Controller.TryExecute(ActionIds.WorkingTreeOpenDiff, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("delete/modify", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Contains("not for the editor", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>tm", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileWithoutMarkersLeft_WhenOpening_ThenTheReadersReasonIsShown()
    {
        using var repository = TestRepositories.Conflicted();
        repository.WriteFile(ConflictPath, "resolved by hand\n");
        using var harness = await Harness.CreateAsync(repository);

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));
        await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("no conflict markers", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACleanFileUnderTheCursor_WhenPressingEnter_ThenTheKeyIsLeftToTheDiff()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        using var harness = await Harness.CreateAsync(repository);

        Assert.False(harness.Controller.TryExecute(ActionIds.WorkingTreeOpenDiff, null));
        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("No conflicts", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnotherPaneFocused_WhenPressingEnter_ThenTheEditorDoesNotOpen()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.False(harness.Controller.TryExecute(ActionIds.WorkingTreeOpenDiff, null));
    }

    [Fact]
    public async Task GivenTheDiffOfAConflictedFileFocused_WhenPressingTheEditorKey_ThenTheEditorOpensOnThatFile()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.WorkingUnstagedDiff));

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsOpen);

        Assert.True(harness.Controller.IsOpen);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Sidebar)]
    [InlineData(PaneKind.Detail)]
    [InlineData(PaneKind.Diff)]
    [InlineData(PaneKind.WorkingStaged)]
    [InlineData(PaneKind.WorkingStagedDiff)]
    [InlineData(PaneKind.WorkingMessage)]
    public async Task GivenAnyPaneFocused_WhenPressingTheEditorKey_ThenTheEditorOpensOnTheConflictedFile(PaneKind pane)
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(pane));

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsOpen);

        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(ConflictPath, harness.Controller.State!.Path);
    }

    [Fact]
    public async Task GivenARebaseStoppedOnAConflictAndTheGraphFocused_WhenPressingTheEditorKey_ThenTheEditorOpens()
    {
        using var repository = TestRepositories.RebaseConflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsOpen);

        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(ConflictPath, harness.Controller.State!.Path);
        Assert.NotEmpty(harness.Controller.State.Hunks);
    }

    [Fact]
    public async Task GivenTwoConflictedFilesAndTheGraphFocused_WhenPressingTheEditorKey_ThenTheFirstOpens()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("a.txt", "a\n"), new TestFile("b.txt", "b\n"));
        repository.CheckoutNewBranch("other")
            .Commit("Other", new TestFile("a.txt", "a other\n"), new TestFile("b.txt", "b other\n"));
        repository.Checkout("main")
            .Commit("Main", new TestFile("a.txt", "a main\n"), new TestFile("b.txt", "b main\n"))
            .MergeExpectingConflict("other");
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsOpen);

        Assert.Equal("a.txt", harness.Controller.State!.Path);
    }

    [Fact]
    public async Task GivenNoConflictAndTheGraphFocused_WhenPressingTheEditorKey_ThenItSaysThereIsNothingToResolve()
    {
        using var repository = TestRepositories.Linear(2);
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("No conflicts", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDiffOfACleanFileFocused_WhenPressingTheEditorKey_ThenItOpensNothingAndSaysSo()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "changed\n");
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.WorkingUnstagedDiff));

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("No conflicts", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenPicksMade_WhenPressingEscOnce_ThenItAsksAndTheSecondEscCloses()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);

        harness.Run(ActionIds.NavigateBack);

        Assert.True(harness.Controller.IsOpen);
        Assert.True(harness.Controller.State!.IsDiscardPending);
        harness.Run(ActionIds.NavigateBack);
        Assert.False(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenNoPicks_WhenPressingEsc_ThenItClosesAtOnce()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.NavigateBack);

        Assert.False(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenAnotherOverlayOpen_WhenAnActionComes_ThenTheEditorLeavesItAlone()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.IsOtherOverlayOpen = true;

        Assert.False(harness.Controller.TryExecute(ActionIds.ConflictEditorOurs, null));

        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
        Assert.Null(harness.Controller.CurrentFrame());
    }

    [Fact]
    public async Task GivenTheTabClosed_WhenAnActionComes_ThenTheEditorCloses()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.CloseTab();

        Assert.False(harness.Controller.TryExecute(ActionIds.ConflictEditorOurs, null));
        Assert.False(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenABusyTab_WhenOpening_ThenItIsRefusedAndNothingIsRead()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Gate.Begin(harness.TabId);

        Assert.True(harness.Controller.TryExecute(ActionIds.ConflictOpen, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("Busy", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheEditorOpen_WhenSteppingThroughTheHunks_ThenTheCursorWraps()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorNext);
        Assert.Equal(1, harness.Controller.State!.Index);
        harness.Run(ActionIds.ConflictEditorNext);
        Assert.Equal(0, harness.Controller.State!.Index);
        harness.Run(ActionIds.ConflictEditorPrevious);
        Assert.Equal(1, harness.Controller.State!.Index);
    }

    [Fact]
    public async Task GivenAPickedConflict_WhenTypingInTheResultAndSaving_ThenTheTypedTextIsWrittenAndTheModeFollows()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);

        harness.Run(ActionIds.ConflictEditorEdit);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.Controller.IsEditing);
        Assert.True(harness.Controller.IsResultActive);
        harness.Type("hand");
        harness.Run(ActionIds.ConflictEditorEditNewLine);
        harness.Type("made");
        harness.Run(ActionIds.ConflictEditorEditDone);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Controller.IsEditing);
        Assert.Equal(ConflictPick.Edited, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitAsync();

        Assert.Equal("hand\nmademain\n", File.ReadAllText(harness.FullPath).Replace("\r", string.Empty));
        Assert.Equal([ConflictPath], harness.ResolvedAsked);
    }

    [Fact]
    public async Task GivenAnEdit_WhenPressingEsc_ThenTheTextIsKeptTheModeIsNormalAndUndoTakesItBack()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorEdit);
        harness.Type("typed");

        harness.Run(ActionIds.Cancel);

        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(ConflictPick.Edited, harness.Controller.State!.SelectedPick);
        Assert.Contains("typed", harness.Controller.State.ToText(), StringComparison.Ordinal);
        harness.Run(ActionIds.ConflictEditorUndo);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.ConflictEditorRedo);
        Assert.Equal(ConflictPick.Edited, harness.Controller.State!.SelectedPick);
    }

    [Fact]
    public async Task GivenTwoPicks_WhenUndoingTwiceAndRedoing_ThenThePicksFollowTheStack()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorNext);
        harness.Run(ActionIds.ConflictEditorTheirs);

        harness.Run(ActionIds.ConflictEditorUndo);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.Result.Regions[1].Pick);
        harness.Run(ActionIds.ConflictEditorUndo);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.Result.Regions[0].Pick);
        Assert.False(harness.Controller.State.IsModified);
        harness.Run(ActionIds.ConflictEditorRedo);

        Assert.Equal(ConflictPick.Ours, harness.Controller.State!.Result.Regions[0].Pick);
        Assert.Equal(ConflictPick.None, harness.Controller.State.Result.Regions[1].Pick);
    }

    [Fact]
    public async Task GivenAPick_WhenUntaking_ThenTheHunkIsUnresolvedAndUndoRestoresThePick()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorBoth);

        harness.Run(ActionIds.ConflictEditorUntake);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.ConflictEditorUndo);

        Assert.Equal(ConflictPick.Both, harness.Controller.State!.SelectedPick);
    }

    [Fact]
    public async Task GivenAnUnresolvedConflict_WhenUntaking_ThenTheStatusSaysThereIsNothingToUntake()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorUntake);

        Assert.Contains("nothing to untake", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.False(harness.Controller.State!.IsModified);
    }

    [Fact]
    public async Task GivenSelectedLines_WhenUntakingThem_ThenOnlyThoseLinesLeaveTheResultAndNoneSaysSo()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.VisualToggle);
        harness.Run(ActionIds.ConflictEditorTake);
        Assert.Equal(ConflictPick.Lines, harness.Controller.State!.SelectedPick);

        harness.Run(ActionIds.VisualToggle);
        harness.Run(ActionIds.ConflictEditorUntake);

        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
        Assert.False(harness.Controller.IsVisual);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        harness.Run(ActionIds.VisualToggle);
        harness.Run(ActionIds.ConflictEditorUntake);
        Assert.Contains("nothing was untaken", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheEditKey_WhenPressedInAVersionPane_ThenFocusMovesIntoTheResultInInsertModeAndTheStatusSaysSo()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorEdit);

        Assert.True(harness.Controller.IsEditing);
        Assert.True(harness.Controller.IsResultActive);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Contains("Typing in the result at conflict 1", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAClickOnAVersionRow_WhenClicking_ThenThePaneGetsTheCursorAndNothingIsTaken()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorNext);
        harness.Run(ActionIds.ConflictEditorNext);
        var row = harness.Controller.State!.Alignment.FirstRowOf(2) + 1;
        Assert.NotEqual(row, harness.Controller.State.Row);

        var view = harness.ClickVersionRow(ConflictPane.Theirs, row);

        Assert.Null(view);
        Assert.True(harness.LastClickHandled);
        var state = harness.Controller.State!;
        Assert.Equal(ConflictPane.Theirs, state.ActivePane);
        Assert.Equal(row, state.Row);
        Assert.Equal(2, state.Index);
        Assert.Equal(ConflictPick.None, state.SelectedPick);
        Assert.False(state.IsModified);
    }

    [Fact]
    public async Task GivenADoubleClickOnAConflictRow_WhenClicking_ThenThatPanesVersionIsTaken()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorNext);
        var row = harness.Controller.State!.Alignment.FirstRowOf(1);

        harness.ClickVersionRow(ConflictPane.Theirs, row, isDoubleClick: true);

        var state = harness.Controller.State!;
        Assert.Equal(ConflictPick.Theirs, state.Result.Regions[1].Pick);
        Assert.Equal(1, state.ResolvedCount);
        Assert.Equal(ConflictPane.Theirs, state.ActivePane);
    }

    [Fact]
    public async Task GivenADoubleClickOnCommonText_WhenClicking_ThenTheCursorMovesAndNothingIsTaken()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var shown = harness.Controller.State!;
        var commonRow = Enumerable.Range(shown.TopScroll, 8).First(row => shown.Alignment.HunkAt(row) < 0);

        harness.ClickVersionRow(ConflictPane.Ours, commonRow, isDoubleClick: true);

        Assert.Equal(commonRow, harness.Controller.State!.Row);
        Assert.False(harness.Controller.State.IsModified);
    }

    [Fact]
    public async Task GivenADoubleClickOnTheBasePane_WhenClicking_ThenTheCommonAncestorIsTaken()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.ClickVersionRow(ConflictPane.Base, 0, isDoubleClick: true);

        Assert.Equal(ConflictPick.Base, harness.Controller.State!.SelectedPick);
        Assert.Equal(ConflictPane.Base, harness.Controller.State.ActivePane);
    }

    [Fact]
    public async Task GivenAClickOnTheResultAndOnAHeader_WhenClicking_ThenTheClickedPaneIsActive()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        var line = harness.Controller.State!.ResultScroll + 2;
        harness.ClickResultLine(line);
        Assert.True(harness.Controller.IsResultActive);
        Assert.Equal(line, harness.Controller.State!.Result.Editor.CursorRow);
        Assert.Equal(KeyMode.Normal, harness.Mode);

        harness.ClickHeader(ConflictPane.Theirs);
        Assert.Equal(ConflictPane.Theirs, harness.Controller.State!.ActivePane);
        harness.ClickHeader(ConflictPane.Result);
        Assert.True(harness.Controller.IsResultActive);
    }

    [Fact]
    public async Task GivenAVisualSelection_WhenClickingARow_ThenTheSelectionEndsAndTheModeIsNormal()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.VisualToggle);
        Assert.Equal(KeyMode.Visual, harness.Mode);

        harness.ClickVersionRow(ConflictPane.Ours, harness.Controller.State!.Row + 1);

        Assert.False(harness.Controller.IsVisual);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenTypingInTheResult_WhenClickingAPaneOrHeader_ThenTheTypingContinuesUndisturbed()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorEdit);
        var before = harness.Controller.State!;

        harness.ClickVersionRow(ConflictPane.Theirs, 0);
        harness.ClickHeader(ConflictPane.Ours);

        Assert.True(harness.Controller.IsEditing);
        Assert.Equal(before.Result.Editor.CursorRow, harness.Controller.State!.Result.Editor.CursorRow);
        Assert.Equal(ConflictPane.Result, harness.Controller.State.ActivePane);
    }

    [Fact]
    public async Task GivenTypingInTheResult_WhenClickingAResultLine_ThenTheCursorMovesThereAndTypingContinues()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorEdit);

        harness.ClickResultLine(0, 3);
        harness.Type("Z");

        var state = harness.Controller.State!;
        Assert.True(harness.Controller.IsEditing);
        Assert.Equal(0, state.Result.Editor.CursorRow);
        Assert.Equal(4, state.Result.Editor.CursorColumn);
        Assert.Equal('Z', state.Result.Editor.Lines[0][3]);
    }

    [Fact]
    public async Task GivenAClickInTheResultText_WhenClicking_ThenTheCursorIsAtThatColumn()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var line = harness.Controller.State!.ResultScroll;
        var length = harness.Controller.State.Result.Editor.Lines[line].Length;
        Assert.True(length > 4);

        harness.ClickResultLine(line, 4);
        Assert.Equal(4, harness.Controller.State!.Result.Editor.CursorColumn);

        harness.ClickResultLine(line, 30);
        Assert.Equal(length, harness.Controller.State!.Result.Editor.CursorColumn);

        harness.ClickResultLine(line);
        Assert.Equal(0, harness.Controller.State!.Result.Editor.CursorColumn);
    }

    [Fact]
    public async Task GivenADragOverVersionRows_WhenTakingTheSelection_ThenOnlyThoseLinesAreTaken()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var first = harness.Controller.State!.Row;

        Assert.True(harness.DragStart(ConflictPane.Ours, first));
        harness.DragTo(first + 2);

        Assert.True(harness.Controller.IsVisual);
        Assert.Equal(KeyMode.Visual, harness.Mode);
        Assert.Equal(ConflictPane.Ours, harness.Controller.State!.ActivePane);
        Assert.Equal(first, harness.Controller.State.SelectionStart);
        Assert.Equal(first + 2, harness.Controller.State.SelectionEnd);

        harness.Run(ActionIds.ConflictEditorTake);

        var state = harness.Controller.State!;
        Assert.Equal(ConflictPick.Lines, state.SelectedPick);
        Assert.Equal(3, state.Result.Regions[0].Length);
    }

    [Fact]
    public async Task GivenADragPastThePanes_WhenDragging_ThenTheSelectionReachesTheEdgeRows()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var first = harness.Controller.State!.Row;
        Assert.True(harness.DragStart(ConflictPane.Ours, first + 1));

        harness.Controller.OnDragged(0);
        Assert.Equal(harness.Controller.State!.TopScroll, harness.Controller.State.Row);

        harness.Controller.OnDragged(harness.Screen.Height);
        Assert.True(harness.Controller.State!.Row > first + 1);
        Assert.True(harness.Controller.IsVisual);
    }

    [Fact]
    public async Task GivenADragOnTheResultOrAHeader_WhenDragging_ThenNoSelectionStarts()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var state = harness.Controller.State!;
        var layout = ConflictEditorFrameBuilder.LayoutFor(state, harness.Screen.Width, harness.Screen.Height - 1)!;

        Assert.Null(harness.Controller.OnDragStarted(layout.ResultStartLine, 10));
        Assert.Null(harness.Controller.OnDragStarted(1, 5));
        harness.Controller.OnDragged(layout.ResultStartLine + 1);

        Assert.False(harness.Controller.IsVisual);
    }

    [Fact]
    public async Task GivenADragWhileTyping_WhenDragging_ThenNoSelectionStarts()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorEdit);

        Assert.False(harness.DragStart(ConflictPane.Ours, 0));
        Assert.False(harness.Controller.IsVisual);
    }

    [Fact]
    public async Task GivenADoubleClickOnAChip_WhenClicking_ThenItIsHandledButItsViewIsNotReturnedAgain()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var layout = ConflictEditorFrameBuilder.LayoutFor(harness.Controller.State!, harness.Screen.Width, 35)!;
        var column = Enumerable.Range(0, harness.Screen.Width).First(candidate =>
            ConflictEditorFrameBuilder.ChipAt(
                harness.Controller.State!,
                layout.FirstChipLine,
                candidate,
                harness.Screen.Width,
                35) is not null);

        var single = harness.Click(layout.FirstChipLine, column);
        var doubled = harness.Click(layout.FirstChipLine, column, isDoubleClick: true);

        Assert.NotNull(single);
        Assert.Null(doubled);
        Assert.True(harness.LastClickHandled);
    }

    [Fact]
    public async Task GivenAVisualSelection_WhenClickingTheActivePaneHeader_ThenTheSelectionEnds()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.VisualToggle);

        harness.ClickHeader(ConflictPane.Ours);

        Assert.False(harness.Controller.IsVisual);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenAClickOnTheBorderOrAnOtherOverlay_WhenClicking_ThenNothingHappens()
    {
        using var repository = TestRepositories.ConflictedManyHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        var before = harness.Controller.State!;

        harness.Click(0, 10);
        Assert.False(harness.LastClickHandled);
        harness.IsOtherOverlayOpen = true;
        harness.ClickVersionRow(ConflictPane.Theirs, 0);

        Assert.False(harness.LastClickHandled);
        Assert.Same(before, harness.Controller.State);
    }

    [Fact]
    public async Task GivenAnEdit_WhenTheEditorIsClosed_ThenTheKeymapGoesBackToNormal()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorEdit);

        harness.Controller.Close();

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Controller.HandleTextKey(new KeyEvent("x")));
    }

    [Fact]
    public async Task GivenAMergeStyleFile_WhenTakingTheBase_ThenTheBaseIsFoundFromTheStagesAndTaken()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorBase);

        Assert.Equal(ConflictPick.Base, harness.Controller.State!.SelectedPick);
        Assert.Contains("original", harness.Controller.State.ToText(), StringComparison.Ordinal);
        Assert.DoesNotContain("<<<<<<<", harness.Controller.State.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenBothSidesAddedTheFile_WhenTakingTheEmptyBase_ThenTheConflictResolvesToNothing()
    {
        using var repository = AddedOnBothSides();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorBase);

        Assert.Equal(ConflictPick.Base, harness.Controller.State!.SelectedPick);
        Assert.Equal(0, harness.Controller.State.Result.Regions[0].Length);
        Assert.Equal(string.Empty, harness.Controller.State.ToText());
    }

    [Fact]
    public async Task GivenARebaseStopped_WhenDrawingTheEditor_ThenItCoversTheBannerAndStopsAtTheStatusBar()
    {
        using var repository = TestRepositories.RebaseConflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Screen = new ScreenSize(100, 30);
        harness.Send(new OperationLoadedCommand(
            new OperationInfo(RepositoryOperation.Rebase, "x", 1, 1, null, null, null, null)));
        await harness.OpenAsync();

        var frame = harness.Controller.CurrentFrame()!;

        Assert.Equal(0, frame.Row);
        Assert.Equal(harness.Screen.Height - 1, frame.Height);
        Assert.Equal(harness.Screen.Width, frame.Width);
    }

    [Fact]
    public async Task GivenADiffThreeConflict_WhenOpeningOnAWideScreen_ThenTheAncestorShowsInTheBasePane()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git("checkout", "--merge", "--conflict=diff3", "--", ConflictPath);
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        var frame = harness.Controller.CurrentFrame()!;

        var ancestor = harness.Controller.State!.Hunks[0].Base!.TrimEnd('\n').Split('\n')[0];
        Assert.Contains(frame.Lines, line => line.Text.Contains(ancestor, StringComparison.Ordinal));
        Assert.Contains("base", frame.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileWreckedOnDisk_WhenRecreatingAndConfirming_ThenTheMarkersAreBackAndTheEditorIsFresh()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Screen = new ScreenSize(100, 36);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        File.WriteAllText(harness.FullPath, "garbage\n");

        harness.Run(ActionIds.ConflictEditorRecreate);
        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("Re-create the conflict of conflict.txt", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("checkout --merge", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal("garbage\n", File.ReadAllText(harness.FullPath));
        harness.Answer();
        await harness.WaitForStatusAsync("Re-created");

        Assert.Contains("<<<<<<<", File.ReadAllText(harness.FullPath), StringComparison.Ordinal);
        var state = harness.Controller.State!;
        Assert.Equal(1, state.UnresolvedCount);
        Assert.False(state.CanUndo);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
        Assert.True(harness.Refreshes > 0);
    }

    [Fact]
    public async Task GivenTheRecreateConfirmDeclined_WhenAnswering_ThenNothingChanges()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);

        harness.Run(ActionIds.ConflictEditorRecreate);
        harness.Decline();

        Assert.Equal(harness.OriginalText, File.ReadAllText(harness.FullPath));
        Assert.Equal(ConflictPick.Ours, harness.Controller.State!.SelectedPick);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenAFileGitCannotRecreate_WhenConfirming_ThenTheStatusShowsGitsReasonAndTheEditorStays()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository, new RefusingRecreator());
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);

        harness.Run(ActionIds.ConflictEditorRecreate);
        harness.Answer();
        await harness.WaitAsync();

        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(ConflictPick.Ours, harness.Controller.State!.SelectedPick);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("Could not re-create", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Contains("refused", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenTheOpenEditor_WhenMovingBetweenPanesWithTheKeys_ThenTheActivePaneAndViewFollow()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        Assert.Equal(DefaultKeymap.ConflictSideView, harness.Controller.ViewName);

        harness.Run(ActionIds.PaneFocusLeft);
        Assert.Equal(ConflictPane.Base, harness.Controller.State!.ActivePane);
        harness.Run(ActionIds.PaneNext);
        harness.Run(ActionIds.PaneNext);
        harness.Run(ActionIds.PaneNext);
        Assert.Equal(ConflictPane.Result, harness.Controller.State!.ActivePane);
        Assert.Equal(DefaultKeymap.ConflictResultView, harness.Controller.ViewName);
        harness.Run(ActionIds.PaneFocusUp);
        Assert.Equal(ConflictPane.Theirs, harness.Controller.State!.ActivePane);
        harness.Run(ActionIds.PaneFocusDown);
        harness.Run(ActionIds.PanePrevious);
        Assert.Equal(ConflictPane.Theirs, harness.Controller.State!.ActivePane);
    }

    [Fact]
    public async Task GivenTheTakeKeyInEachPane_WhenPressed_ThenThatPanesVersionIsTaken()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Send(new FocusPaneCommand(PaneKind.WorkingUnstaged));
        await harness.OpenAsync();

        harness.Run(ActionIds.PaneFocusRight);
        harness.Run(ActionIds.ConflictEditorTake);
        Assert.Equal(ConflictPick.Theirs, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.PaneFocusLeft);
        harness.Run(ActionIds.PaneFocusLeft);
        harness.Run(ActionIds.ConflictEditorTake);
        Assert.Equal(ConflictPick.Base, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.PaneFocusRight);
        harness.Run(ActionIds.ConflictEditorTake);
        Assert.Equal(ConflictPick.Ours, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.PaneFocusDown);
        harness.Run(ActionIds.ConflictEditorTake);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("ours or theirs pane", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(ConflictPick.Ours, harness.Controller.State!.SelectedPick);
    }

    [Fact]
    public async Task GivenAVisualSelection_WhenTakingItAndLeavingIt_ThenTheModeFollowsAndOnlyTheLinesAreTaken()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.VisualToggle);
        Assert.Equal(KeyMode.Visual, harness.Mode);
        harness.Run(ActionIds.ListDown);
        harness.Run(ActionIds.Cancel);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Controller.IsVisual);
        harness.Run(ActionIds.VisualToggle);
        harness.Run(ActionIds.ListDown);
        harness.Run(ActionIds.ListDown);
        harness.Run(ActionIds.ConflictEditorTake);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        var state = harness.Controller.State!;
        Assert.Equal(ConflictPick.Lines, state.SelectedPick);
        Assert.Equal(3, state.Result.Regions[0].Length);
        harness.Run(ActionIds.ConflictEditorUndo);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
    }

    [Fact]
    public async Task GivenASelectionWithNoLinesOfTheConflict_WhenTakingIt_ThenTheStatusSaysSoAndNothingChanges()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ListTop);
        harness.Run(ActionIds.VisualToggle);

        harness.Run(ActionIds.ConflictEditorTake);

        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("no line of the current conflict", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.False(harness.Controller.State!.IsModified);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenTheResultPaneInNormalMode_WhenDeletingLines_ThenTheTextChangesAndUndoRestoresIt()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.PaneFocusDown);

        harness.Run(ActionIds.WorkingTreeMessageDeleteLine);
        harness.Run(ActionIds.WorkingTreeMessageDeleteLine);

        var state = harness.Controller.State!;
        Assert.Equal(2, state.UndoStack!.Count);
        Assert.DoesNotContain("<<<<<<<", state.Result.Editor.Lines[state.Result.Regions[0].Start]);
        harness.Run(ActionIds.ConflictEditorUndo);
        harness.Run(ActionIds.ConflictEditorUndo);
        Assert.Equal(harness.OriginalText, harness.Controller.State!.ToText());
    }

    [Fact]
    public async Task GivenTextTypedOutsideAConflict_WhenPickingAndSaving_ThenTheTypedTextIsInTheFile()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.PaneFocusDown);
        harness.Run(ActionIds.ListTop);
        harness.Run(ActionIds.WorkingTreeMessageAppendAtEnd);
        harness.Type("!");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.PaneFocusUp);
        harness.Run(ActionIds.ConflictEditorAllOurs);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitAsync();

        var saved = File.ReadAllText(harness.FullPath).Replace("\r", string.Empty);
        Assert.StartsWith("l1!\n", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("<<<<<<<", saved, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAConfirmedSave_WhenTheResultChangesBeforeTheWriteEnds_ThenTheConfirmedTextIsWritten()
    {
        using var repository = TwoHunks();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorSave);

        harness.Answer();
        harness.Run(ActionIds.ConflictEditorTheirs);
        await harness.WaitAsync();

        var saved = File.ReadAllText(harness.FullPath).Replace("\r", string.Empty);
        Assert.Contains("II\n", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("two\n", saved, StringComparison.Ordinal);
        Assert.Empty(harness.ResolvedAsked);
    }

    [Fact]
    public async Task GivenTheWheelAndHalfPageActions_WhenRunning_ThenTheCursorMovesByTheCount()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ListTop);

        Assert.True(harness.Controller.TryExecute(ActionIds.ListDown, 3));

        Assert.Equal(3, harness.Controller.State!.Row);
    }

    [Fact]
    public async Task GivenInsertMode_WhenAnotherActionComes_ThenItIsIgnoredAndTheModeStays()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorEdit);

        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorSave);

        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.Controller.IsEditing);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenAFileMixingLineEndings_WhenOpening_ThenTheStatusWarns()
    {
        using var repository = TestRepositories.Conflicted();
        var text = File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath));
        var crlf = text.Replace("\n", "\r\n", StringComparison.Ordinal);
        repository.WriteFile(ConflictPath, crlf.Replace("main\r\n", "main\n", StringComparison.Ordinal));
        using var harness = await Harness.CreateAsync(repository);

        await harness.OpenAsync();

        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("mixes CRLF and LF", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    private static TestRepository Stopped(string operation) => operation switch
    {
        "merge" => TestRepositories.Conflicted(),
        "rebase" => TestRepositories.RebaseConflicted(),
        _ => TestRepositories.CherryPickConflicted(),
    };

    // The marked file continues the operation the way the banner's continue does.
    private static void Finish(TestRepository repository, string operation)
    {
        repository.Git("add", ConflictPath);
        if (operation == "merge")
        {
            repository.Git("-c", "core.editor=true", "commit", "--no-edit");
        }
        else
        {
            repository.Git("-c", "core.editor=true", operation, "--continue");
        }
    }

    [Fact]
    public async Task GivenALongFile_WhenMovingTheCursor_ThenAllPanesScrollTogetherAndStayAligned()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        harness.Screen = new ScreenSize(100, 20);
        await harness.OpenAsync();

        harness.Run(ActionIds.ListBottom);
        var bottom = harness.Controller.State!;
        harness.Run(ActionIds.ListTop);
        var top = harness.Controller.State!;
        harness.Run(ActionIds.ListHalfPageDown);
        var half = harness.Controller.State!;

        Assert.True(bottom.TopScroll > 0);
        Assert.True(bottom.ResultScroll > 0);
        Assert.Equal(0, top.TopScroll);
        Assert.Equal(0, top.ResultScroll);
        Assert.True(half.Row > 0);
        Assert.NotNull(harness.Controller.CurrentFrame());
    }

    // Every other line from 5 to 35 changes; git joins changes separated by one line into a single conflict.
    private static string Rewritten(string common, string prefix)
    {
        var lines = common.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var number = 5; number <= 35; number += 2)
        {
            lines[number - 1] = $"{prefix}{number}";
        }

        return string.Concat(lines.Select(line => $"{line}\n"));
    }

    private static TestRepository LongHunk()
    {
        var common = string.Concat(Enumerable.Range(1, 40).Select(number => $"c{number}\n"));
        var main = Rewritten(common, "main");
        var other = Rewritten(common, "other");
        var repository = TestRepository.Create()
            .Commit("Initial", new TestFile(ConflictPath, common))
            .CheckoutNewBranch("other")
            .Commit("Other", new TestFile(ConflictPath, other))
            .Checkout("main")
            .Commit("Main", new TestFile(ConflictPath, main));

        return repository.MergeExpectingConflict("other");
    }

    // Both branches add the same new file with different contents (add/add: stages 2 and 3, no base).
    private static TestRepository AddedOnBothSides()
    {
        var repository = TestRepository.Create().Commit("Initial", new TestFile("base.txt", "x\n"));
        repository.CheckoutNewBranch("other").Commit("Add", new TestFile(ConflictPath, "other\n"));

        return repository.Checkout("main").Commit("Add", new TestFile(ConflictPath, "main\n"))
            .MergeExpectingConflict("other");
    }

    private static TestRepository TwoHunks()
    {
        var other = TwoHunkBase.Replace("l2\n", "two\n").Replace("l11\n", "eleven\n");
        var main = TwoHunkBase.Replace("l2\n", "II\n").Replace("l11\n", "XI\n");
        var repository = TestRepository.Create()
            .Commit("Initial", new TestFile(ConflictPath, TwoHunkBase))
            .CheckoutNewBranch("other")
            .Commit("Other", new TestFile(ConflictPath, other))
            .Checkout("main")
            .Commit("Main", new TestFile(ConflictPath, main));

        return repository.MergeExpectingConflict("other");
    }

    private sealed class RefusingRecreator : IConflictRecreator
    {
        public Task RecreateAsync(
            RepositoryLocation location,
            string path,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the file has no conflict to re-create, refused");
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];

        private Harness(TestRepository repository, RepositoryLocation location, IConflictRecreator? recreator)
        {
            Runner = repository.CreateIsolatedRunner();
            Location = location;
            FullPath = Path.Combine(repository.WorkingTreePath, ConflictPath);
            OriginalText = File.Exists(FullPath) ? File.ReadAllText(FullPath) : string.Empty;
            _host.Dispatch(new OpenRepositoryCommand(location));
            TabId = _host.Workspace.ActiveTabId!.Value;
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Controller = new ConflictEditorController(
                _host,
                new ConflictReader(Runner),
                new ConflictWriter(),
                recreator ?? new ConflictRecreator(Runner),
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                AskResolved,
                _ =>
                {
                    Refreshes++;

                    return true;
                },
                () => IsOtherOverlayOpen,
                () => Screen,
                GraphGlyphSet.Unicode,
                SetMode,
                () => { },
                Gate);
        }

        public GitRunner Runner { get; }

        public RepositoryLocation Location { get; }

        public ConflictEditorController Controller { get; }

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public List<string> ResolvedAsked { get; } = [];

        public int Refreshes { get; private set; }

        public ScreenSize Screen { get; set; } = new(120, 36);

        public bool IsOtherOverlayOpen { get; set; }

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public TabId TabId { get; }

        public UiPump Pump { get; }

        public string FullPath { get; }

        public string OriginalText { get; }

        public static async Task<Harness> CreateAsync(TestRepository repository, IConflictRecreator? recreator = null)
        {
            var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
            var token = TestContext.Current.CancellationToken;
            var location = await discovery.DiscoverAsync(repository.WorkingTreePath, token);
            var harness = new Harness(repository, location, recreator);
            await harness.ReloadAsync();

            return harness;
        }

        public async Task ReloadAsync()
        {
            var token = TestContext.Current.CancellationToken;
            var refs = new RefReader(Runner);
            Send(new HeadLoadedCommand(await refs.ReadHeadAsync(Location, token)));
            var status = await new StatusReader(Runner).ReadStatusAsync(Location, false, UntrackedFilesMode.All, token);
            Send(new WorkingTreeLoadedCommand(status));
            Send(new FocusPaneCommand(PaneKind.WorkingUnstaged));
            Send(new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 0));
        }

        public Task WaitForStatusAsync(string prefix) => Pump.RunUntilAsync(() =>
            !Gate.IsAnyBusy
            && Statuses.Log.Latest is { } latest
            && latest.Text.StartsWith(prefix, StringComparison.Ordinal));

        public void CloseTab() => _host.Dispatch(new CloseTabCommand(TabId));

        public void Send(RepositoryCommand command) => _host.Dispatch(new TabCommand(TabId, command));

        // Opening reads the file on a worker: wait until the editor shows or the status reported a refusal.
        public async Task OpenAsync()
        {
            Assert.True(Controller.TryExecute(ActionIds.WorkingTreeOpenDiff, null));
            await Pump.RunUntilAsync(() => Controller.IsOpen || Statuses.Log.Latest is not null);
            Assert.True(Controller.IsOpen, Statuses.Log.Latest?.Text);
        }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Controller.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        public void Run(ActionId action) => Assert.True(Controller.TryExecute(action, null));

        public bool LastClickHandled { get; private set; }

        public string? Click(int line, int column, bool isDoubleClick = false)
        {
            var view = Controller.OnLineClicked(line, column, out var handled, isDoubleClick);
            LastClickHandled = handled;

            return view;
        }

        // A click in the middle of a pane, found through the same layout the screen uses.
        public string? ClickVersionRow(ConflictPane pane, int row, bool isDoubleClick = false)
        {
            var state = Controller.State!;
            var width = Screen.Width;
            var column = 1 + (((int)pane - (int)ConflictPane.Base) * (width / 3)) + 4;

            return Click(
                ConflictEditorLayout.VersionStartLine + row - state.TopScroll,
                column,
                isDoubleClick);
        }

        public void ClickResultLine(int line, int textColumn = 0)
        {
            var state = Controller.State!;
            var layout = ConflictEditorFrameBuilder.LayoutFor(state, Screen.Width, Screen.Height - 1)!;

            var frameLine = layout.ResultStartLine + line - state.ResultScroll;

            // The last column that still maps to text column 0 is the first cell of the text.
            var origin = Enumerable.Range(1, Screen.Width - 2)
                .Last(column => ConflictEditorFrameBuilder.TargetAt(state, frameLine, column, Screen.Width, Screen.Height - 1)
                    is { Column: 0 } target && target.Kind == ConflictTargetKind.ResultLine
                    && (column == 1 || ConflictEditorFrameBuilder.TargetAt(state, frameLine, column + 1, Screen.Width, Screen.Height - 1)
                        is { Column: <= 1 }));

            Click(frameLine, origin + textColumn);
        }

        public int LineOfRow(int row) =>
            ConflictEditorLayout.VersionStartLine + row - Controller.State!.TopScroll;

        // The drag start goes through the mouse map to visual.toggle (checked against DefaultMouseMap by MouseMapTests).
        public bool DragStart(ConflictPane pane, int row)
        {
            var view = Controller.OnDragStarted(
                LineOfRow(row),
                1 + (((int)pane - (int)ConflictPane.Base) * (Screen.Width / 3)) + 4);
            if (view is null)
            {
                return false;
            }

            Assert.Equal("ConflictSide", view);
            Run(ActionIds.VisualToggle);

            return true;
        }

        public void DragTo(int row) => Controller.OnDragged(LineOfRow(row));

        public void ClickHeader(ConflictPane pane)
        {
            var state = Controller.State!;
            var layout = ConflictEditorFrameBuilder.LayoutFor(state, Screen.Width, Screen.Height - 1)!;
            if (pane == ConflictPane.Result)
            {
                Click(layout.ResultStartLine - 1, 10);

                return;
            }

            Click(1, 1 + (((int)pane - (int)ConflictPane.Base) * (Screen.Width / 3)) + 4);
        }

        public void Answer()
        {
            var answers = _answers.ToList();
            _answers.Clear();
            foreach (var answer in answers)
            {
                answer();
            }
        }

        public void Decline() => _answers.Clear();

        public Task WaitAsync() => Pump.RunUntilAsync(() => !Gate.IsAnyBusy && Statuses.Log.Latest is not null);

        public void Dispose()
        {
        }

        private void SetMode(KeyMode mode) => Mode = mode;

        private bool Confirm(ConfirmPrompt prompt, Action onConfirmed)
        {
            Prompts.Add(prompt);
            _answers.Add(onConfirmed);

            return true;
        }

        private string AskResolved(RepositoryState tab, string path)
        {
            ResolvedAsked.Add(path);

            return string.Empty;
        }
    }
}
