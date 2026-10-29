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
        Assert.Contains("every hunk resolved", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
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
        Assert.Contains("1 unresolved hunk out of 2", prompt.Lines[0], StringComparison.Ordinal);
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
        Assert.Contains("1 hunk still unresolved", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
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
    public async Task GivenBothSidesAddedTheFile_WhenOpening_ThenTheEditorStaysClosedAndNamesTheMergetool()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("base.txt", "x\n"));
        repository.CheckoutNewBranch("other").Commit("Add", new TestFile("new.txt", "other\n"));
        repository.Checkout("main").Commit("Add", new TestFile("new.txt", "main\n")).MergeExpectingConflict("other");
        using var harness = await Harness.CreateAsync(repository);

        Assert.True(harness.Controller.TryExecute(ActionIds.WorkingTreeOpenDiff, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("add/add", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
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
        harness.Run(ActionIds.ListDown);
        Assert.Equal(0, harness.Controller.State!.Index);
        harness.Run(ActionIds.ConflictEditorPrevious);
        Assert.Equal(1, harness.Controller.State!.Index);
    }

    [Fact]
    public async Task GivenAPickedHunk_WhenEditingTypingAndSaving_ThenTheTypedTextIsWrittenAndTheModeFollows()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);

        harness.Run(ActionIds.ConflictEditorEdit);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.Controller.IsEditing);
        harness.Type("hand");
        harness.Run(ActionIds.ConflictEditorEditNewLine);
        harness.Type("made");
        harness.Run(ActionIds.ConflictEditorEditDone);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Controller.IsEditing);
        Assert.Equal(ConflictPick.Edited, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.ConflictEditorSave);
        await harness.WaitAsync();

        Assert.Equal("main\nhand\nmade\n", File.ReadAllText(harness.FullPath).Replace("\r", string.Empty));
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
        Assert.Equal(ConflictPick.None, harness.Controller.State!.Picks[1]);
        harness.Run(ActionIds.ConflictEditorUndo);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.Picks[0]);
        Assert.False(harness.Controller.State.IsModified);
        harness.Run(ActionIds.ConflictEditorRedo);

        Assert.Equal(ConflictPick.Ours, harness.Controller.State!.Picks[0]);
        Assert.Equal(ConflictPick.None, harness.Controller.State.Picks[1]);
    }

    [Fact]
    public async Task GivenAPick_WhenResetting_ThenTheHunkIsUnresolvedAndUndoRestoresThePick()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorBoth);

        harness.Run(ActionIds.ConflictEditorReset);
        Assert.Equal(ConflictPick.None, harness.Controller.State!.SelectedPick);
        harness.Run(ActionIds.ConflictEditorUndo);

        Assert.Equal(ConflictPick.Both, harness.Controller.State!.SelectedPick);
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
    public async Task GivenThreePanes_WhenPressingTheBaseKey_ThenNothingChangesNotEvenAPendingDiscard()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.NavigateBack);

        harness.Run(ActionIds.ConflictEditorBase);

        Assert.False(harness.Controller.State!.IsBaseShown);
        Assert.True(harness.Controller.State.IsDiscardPending);
    }

    [Fact]
    public async Task GivenTheBaseKey_WhenPressedOnAMergeStyleFile_ThenTheBasePaneToggles()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Screen = new ScreenSize(100, 36);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorBase);

        Assert.True(harness.Controller.State!.IsBaseShown);
        var lines = harness.Controller.CurrentFrame()!.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains("base  common ancestor", StringComparison.Ordinal));
        Assert.Contains("original", lines[header + 1], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("merge.conflictStyle", StringComparison.Ordinal));
        harness.Run(ActionIds.ConflictEditorBase);
        Assert.False(harness.Controller.State!.IsBaseShown);
    }

    [Fact]
    public async Task GivenARebaseStoppedOnTheConflict_WhenDrawingTheEditor_ThenTheBannerRowStaysFree()
    {
        using var repository = TestRepositories.RebaseConflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Screen = new ScreenSize(100, 30);
        harness.Send(new OperationLoadedCommand(
            new OperationInfo(RepositoryOperation.Rebase, "x", 1, 1, null, null, null, null)));
        await harness.OpenAsync();

        var frame = harness.Controller.CurrentFrame()!;

        // Rows: the status bar is the last one and the banner the one above it.
        Assert.True(frame.Row + frame.Height <= harness.Screen.Height - 2);
    }

    [Fact]
    public async Task GivenADiffThreeConflict_WhenOpeningOnAWideScreen_ThenTheAncestorShowsWithoutPressingB()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git("checkout", "--merge", "--conflict=diff3", "--", ConflictPath);
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        var frame = harness.Controller.CurrentFrame()!;

        var lines = frame.Lines.Select(line => line.Text).ToList();
        var header = lines.FindIndex(line => line.Contains("base  common ancestor", StringComparison.Ordinal));
        Assert.True(header > 0);
        var ancestor = harness.Controller.State!.Hunks[0].Base!.TrimEnd('\n').Split('\n')[0];
        Assert.Contains(ancestor, lines[header + 1], StringComparison.Ordinal);
        Assert.False(harness.Controller.State.IsBaseShown);
    }

    [Fact]
    public async Task GivenAFileWreckedOnDisk_WhenRecreatingAndConfirming_ThenTheMarkersAreBackAndTheEditorIsFresh()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Screen = new ScreenSize(100, 36);
        await harness.OpenAsync();
        harness.Run(ActionIds.ConflictEditorOurs);
        harness.Run(ActionIds.ConflictEditorBase);
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
        Assert.True(state.IsBaseShown);
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
    public async Task GivenALongHunk_WhenScrollingAndJumping_ThenTheSidesScrollAndAnotherConflictResets()
    {
        using var repository = LongHunk();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ConflictEditorScrollDown);
        var scrolled = harness.Controller.State!.SideScroll;
        harness.Run(ActionIds.ConflictEditorScrollUp);
        var back = harness.Controller.State!.SideScroll;
        harness.Run(ActionIds.ConflictEditorNextDiff);
        var jumped = harness.Controller.State!.SideScroll;
        harness.Run(ActionIds.ConflictEditorNext);

        Assert.True(scrolled > 0);
        Assert.Equal(0, back);
        Assert.Equal(2, jumped);
        Assert.Equal(0, harness.Controller.State!.SideScroll);
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
