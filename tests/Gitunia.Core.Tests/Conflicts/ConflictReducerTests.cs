using Gitunia.Core.Conflicts;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Running;
using Gitunia.Core.Tests.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

// The conflict editor through the root reducer: state plus command gives state plus effects, nothing else.
public class ConflictReducerTests
{
    private const string Path = "file.txt";
    private const string Text =
        "top\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n";
    private const string Single = "top\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nend\n";

    private static readonly ConflictViewports Tall = new(new(10, 6), new(10, 6), new(10, 6), new(10, 6));

    [Fact]
    public void GivenAConflictedFileUnderTheCursor_WhenOpening_ThenItsReadIsRequestedAndTheTabIsBusy()
    {
        var state = Loaded(Entry(Path, ConflictKind.BothModified));

        var update = AppReducer.Reduce(state, new ConflictOpenRequested(true, Tall));

        var read = Assert.IsType<ReadConflictEffect>(Assert.Single(update.Effects));
        Assert.Equal(Path, read.Path);
        Assert.Equal(1, read.Generation);
        Assert.Equal(ConflictKind.BothModified, read.Conflict);
        Assert.True(update.State.Workspace.ActiveTab!.Running.IsRunning(RunningOperationKind.Conflicts));
        Assert.False(update.State.Shell.Conflict.IsOpen);
    }

    [Theory]
    [InlineData(RunningOperationKind.Staging)]
    [InlineData(RunningOperationKind.Discard)]
    [InlineData(RunningOperationKind.Commit)]
    [InlineData(RunningOperationKind.Stash)]
    [InlineData(RunningOperationKind.Integration)]
    [InlineData(RunningOperationKind.Pick)]
    [InlineData(RunningOperationKind.Rebase)]
    [InlineData(RunningOperationKind.Branches)]
    [InlineData(RunningOperationKind.Sync)]
    public void GivenAnotherWorkingTreeOperationRuns_WhenOpeningTheEditor_ThenItIsRefusedAsBusy(
        RunningOperationKind running)
    {
        var state = Loaded(Entry(Path, ConflictKind.BothModified));
        var tab = state.Workspace.ActiveTab!;
        var busy = AppReducerTestState.WithRunning(state, tab.Id, running);

        var update = AppReducer.Reduce(busy, new ConflictOpenRequested(true, Tall));

        Assert.Equal(ConflictNotices.Busy, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenAReadInFlight_WhenOpeningAgain_ThenItIsRefusedAsBusy()
    {
        var reading = Reading();

        var update = AppReducer.Reduce(reading.State, new ConflictOpenRequested(true, Tall));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(ConflictNotices.Busy, post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenAFileTheEditorRefuses_WhenOpening_ThenTheStatusNamesTheMergetoolAndNothingIsRead()
    {
        var state = Loaded(Entry(Path, ConflictKind.DeletedByThem));

        var update = AppReducer.Reduce(state, new ConflictOpenRequested(true, Tall));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Contains("they deleted it", post.Text, StringComparison.Ordinal);
        Assert.Contains("<space>em", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoConflictedFile_WhenOpeningByTheEditorKey_ThenTheStatusSaysThereIsNothingToResolve()
    {
        var update = AppReducer.Reduce(Loaded(), new ConflictOpenRequested(true, Tall));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(ConflictNotices.NoConflicts, post.Text);
    }

    [Fact]
    public void GivenEnterOnACleanFile_WhenOpening_ThenNothingHappensAndTheKeyIsLeftToTheDiff()
    {
        var update = AppReducer.Reduce(Loaded(), new ConflictOpenRequested(false, Tall));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheResultOfTheRead_WhenItArrives_ThenTheEditorOpensCenteredOnTheFirstConflictAndTheBusyMarkEnds()
    {
        var reading = Reading();

        var update = AppReducer.Reduce(reading.State, Read(reading.State, Text));

        var editor = update.State.Shell.Conflict.Editor!;
        Assert.Equal(2, editor.Hunks.Count);
        Assert.Equal(Path, editor.Path);
        Assert.False(update.State.Workspace.ActiveTab!.Running.IsRunning(RunningOperationKind.Conflicts));
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAReadForAnEditorThatIsGone_WhenItArrives_ThenItIsDropped()
    {
        var reading = Reading();
        var stale = Read(reading.State, Text) with { Generation = 99 };

        var update = AppReducer.Reduce(reading.State, stale);

        Assert.False(update.State.Shell.Conflict.IsOpen);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOverlayOpenedMeanwhile_WhenTheReadArrives_ThenTheEditorStaysClosedAndSaysWhy()
    {
        var reading = Reading();
        var asked = AppReducer.Reduce(
            reading.State,
            new ConfirmRequestedCommand(new ConfirmPrompt("t", ["l"], "c", "go"), new ConflictTabClosed(), null));

        var update = AppReducer.Reduce(asked.State, Read(asked.State, Text));

        Assert.False(update.State.Shell.Conflict.IsOpen);
        Assert.Equal(ConflictNotices.Covered, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheTabClosedWhileTheFileWasRead_WhenTheReadArrives_ThenTheEditorStaysClosedAndSaysSo()
    {
        var reading = Reading();
        var read = Read(reading.State, Text);
        var closed = AppReducer.Reduce(reading.State, new CloseTabCommand(read.TabId)).State;

        var update = AppReducer.Reduce(closed, read);

        Assert.False(update.State.Shell.Conflict.IsOpen);
        Assert.Equal(ConflictNotices.Changed, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheMixedLineEndings_WhenTheFileOpens_ThenTheStatusWarnsAboutTheSave()
    {
        var reading = Reading();

        var mixed = Single.Replace("A1\n", "A1\r\n", StringComparison.Ordinal);

        var update = AppReducer.Reduce(reading.State, Read(reading.State, mixed));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Contains("mixes CRLF and LF", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOpenEditor_WhenPickingTheNextConflict_ThenTheKeymapStaysNormalAndNoEffectIsRaised()
    {
        var state = Opened(Text);

        var update = Press(state, ActionIds.ConflictEditorOurs);

        Assert.Equal(ConflictPick.Ours, update.State.Shell.Conflict.Editor!.SelectedPick);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheEditKey_WhenPressed_ThenTheKeymapGoesToInsertAndEscGoesBackToNormal()
    {
        var typing = Press(Opened(Text), ActionIds.ConflictEditorEdit);

        Assert.Contains(typing.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Insert });
        Assert.True(typing.State.Shell.Conflict.Editor!.IsEditing);

        var kept = Press(typing.State, ActionIds.Cancel);

        Assert.Contains(kept.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
        Assert.False(kept.State.Shell.Conflict.Editor!.IsEditing);
    }

    [Fact]
    public void GivenAPick_WhenPressingEscOnce_ThenItAsksAndTheSecondEscClosesWithTheKeymapNormal()
    {
        var picked = Press(Opened(Text), ActionIds.ConflictEditorOurs).State;

        var asked = Press(picked, ActionIds.Cancel);
        var closed = Press(asked.State, ActionIds.Cancel);

        Assert.True(asked.State.Shell.Conflict.Editor!.IsDiscardPending);
        Assert.False(closed.State.Shell.Conflict.IsOpen);
    }

    [Fact]
    public void GivenNoPicks_WhenSaving_ThenItIsRefusedWithoutAConfirm()
    {
        var update = Press(Opened(Text), ActionIds.ConflictEditorSave);

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(ConflictNotices.NothingPicked, post.Text);
    }

    [Fact]
    public void GivenEveryConflictResolved_WhenSaving_ThenTheWriteIsRequestedWithoutAConfirm()
    {
        var picked = Press(Opened(Single), ActionIds.ConflictEditorTheirs).State;

        var update = Press(picked, ActionIds.ConflictEditorSave);

        var write = Assert.IsType<WriteConflictEffect>(Assert.Single(update.Effects));
        Assert.Equal("top\nB1\nend\n", write.Text);
        Assert.Equal(Single, write.OriginalText);
        Assert.Equal("Saved file.txt", write.Message);
        Assert.True(write.IsComplete);
        Assert.True(update.State.Workspace.ActiveTab!.Running.IsRunning(RunningOperationKind.Conflicts));
    }

    [Fact]
    public void GivenAnUnresolvedConflict_WhenSaving_ThenTheConfirmNamesTheCountAndNothingIsWrittenYet()
    {
        var picked = Press(Opened(Text), ActionIds.ConflictEditorTheirs).State;

        var update = Press(picked, ActionIds.ConflictEditorSave);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Contains("1 unresolved conflict out of 2", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("git checkout --merge", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.IsType<ConflictSaveConfirmed>(confirm.OnAccepted);
    }

    [Fact]
    public void GivenTheSaveConfirmAccepted_WhenItArrives_ThenTheTextConfirmedIsWrittenAsAPartialSave()
    {
        var picked = Press(Opened(Text), ActionIds.ConflictEditorTheirs).State;
        var confirm = (ShowConfirmEffect)Press(picked, ActionIds.ConflictEditorSave).Effects.Single();

        var update = AppReducer.Reduce(picked, confirm.OnAccepted);

        var write = Assert.IsType<WriteConflictEffect>(Assert.Single(update.Effects));
        Assert.False(write.IsComplete);
        Assert.Equal("Saved file.txt: 1 conflict still unresolved", write.Message);
        Assert.Contains("<<<<<<<", write.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheTabClosedWhileTheSaveDialogIsOpen_WhenItIsAnswered_ThenNothingIsWritten()
    {
        var picked = Press(Opened(Text), ActionIds.ConflictEditorTheirs).State;
        var confirm = (ShowConfirmEffect)Press(picked, ActionIds.ConflictEditorSave).Effects.Single();
        var request = new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, null);
        var asked = AppReducer.Reduce(picked, request).State;
        var tabId = asked.Shell.Conflict.Editor!.TabId;

        var closed = AppReducer.Reduce(asked, new CloseTabCommand(tabId));
        var answered = AppReducer.Reduce(closed.State, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.False(closed.State.Shell.Conflict.IsOpen);
        Assert.DoesNotContain(answered.Effects, effect => effect is WriteConflictEffect);
    }

    [Fact]
    public void GivenTheEditorClosedWhileTheSaveDialogIsOpen_WhenItIsAnswered_ThenNothingIsWritten()
    {
        var picked = Press(Opened(Text), ActionIds.ConflictEditorTheirs).State;
        var confirm = (ShowConfirmEffect)Press(picked, ActionIds.ConflictEditorSave).Effects.Single();
        var closed = AppReducer.Reduce(picked, new ConflictTabClosed()).State;

        var update = AppReducer.Reduce(closed, confirm.OnAccepted);

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheTabClosedWhileTheRecreateDialogIsOpen_WhenItIsAnswered_ThenNothingIsRecreated()
    {
        var state = Opened(Text);
        var confirm = (ShowConfirmEffect)Press(state, ActionIds.ConflictEditorRecreate).Effects.Single();
        var request = new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, null);
        var asked = AppReducer.Reduce(state, request).State;
        var closed = AppReducer.Reduce(asked, new CloseTabCommand(asked.Shell.Conflict.Editor!.TabId)).State;

        var answered = AppReducer.Reduce(closed, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.DoesNotContain(answered.Effects, effect => effect is RecreateConflictEffect);
    }

    [Fact]
    public void GivenTheRecreateConfirmAccepted_WhenItArrives_ThenTheRecreateIsRequestedForTheFile()
    {
        var state = Opened(Text);
        var confirm = (ShowConfirmEffect)Press(state, ActionIds.ConflictEditorRecreate).Effects.Single();
        Assert.Contains("Re-create the conflict of file.txt", confirm.Prompt.Lines[0], StringComparison.Ordinal);

        var update = AppReducer.Reduce(state, confirm.OnAccepted);

        var recreate = Assert.IsType<RecreateConflictEffect>(Assert.Single(update.Effects));
        Assert.Equal(Path, recreate.Path);
    }

    [Fact]
    public void GivenTypingInTheResult_WhenSaving_ThenTheTypedTextIsKeptAndTheSaveGoesOn()
    {
        var picked = Press(Opened(Single), ActionIds.ConflictEditorTheirs).State;
        var typing = Press(picked, ActionIds.ConflictEditorEdit).State;
        var typed = AppReducer.Reduce(typing, new ConflictKeyTyped(new KeyEvent("x"), Tall)).State;

        var update = Press(typed, ActionIds.ConflictEditorSave);

        var write = Assert.IsType<WriteConflictEffect>(update.Effects.Single(effect => effect is WriteConflictEffect));
        Assert.Contains("x", write.Text, StringComparison.Ordinal);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenAFullySavedFile_WhenTheWriteSucceeds_ThenTheEditorClosesAndTheFileIsMarkedResolved()
    {
        var state = Opened(Single);
        var tabId = state.Shell.Conflict.Editor!.TabId;

        var update = AppReducer.Reduce(state, new ConflictSaved(tabId, Path, "Saved file.txt", true));

        Assert.False(update.State.Shell.Conflict.IsOpen);
        Assert.Contains(update.Effects, effect => effect is StageResolvedConflictEffect { Path: Path });
        Assert.DoesNotContain(update.Effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenAPartialSave_WhenTheWriteSucceeds_ThenTheEditorClosesAndTheTabIsRefreshedNotStaged()
    {
        var state = Opened(Text);
        var tabId = state.Shell.Conflict.Editor!.TabId;

        var update = AppReducer.Reduce(state, new ConflictSaved(tabId, Path, "Saved", false));

        Assert.False(update.State.Shell.Conflict.IsOpen);
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect);
        Assert.DoesNotContain(update.Effects, effect => effect is StageResolvedConflictEffect);
    }

    [Fact]
    public void GivenTheSaveResultOfAnotherTab_WhenItArrives_ThenTheOpenEditorStaysOpen()
    {
        var state = Opened(Text);
        var other = new TabId(state.Shell.Conflict.Editor!.TabId.Value + 100);

        var update = AppReducer.Reduce(state, new ConflictSaved(other, Path, "Saved", false));

        Assert.True(update.State.Shell.Conflict.IsOpen);
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenAFailedSave_WhenItsResultArrives_ThenTheEditorStaysWithItsPicksAndTheReasonShows()
    {
        var picked = Press(Opened(Single), ActionIds.ConflictEditorTheirs).State;
        var tabId = picked.Shell.Conflict.Editor!.TabId;

        var failed = new ConflictOperationFailed(tabId, "Could not save", StatusSeverity.Error);

        var update = AppReducer.Reduce(picked, failed);

        Assert.True(update.State.Shell.Conflict.IsOpen);
        Assert.Equal(1, update.State.Shell.Conflict.Editor!.ResolvedCount);
        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Severity);
    }

    [Fact]
    public void GivenTheConflictRecreated_WhenTheResultArrives_ThenTheEditorIsFreshAndTheTabIsRefreshed()
    {
        var picked = Press(Opened(Text), ActionIds.ConflictEditorOurs).State;
        var tabId = picked.Shell.Conflict.Editor!.TabId;
        var result = ConflictReadResult.OfText(ConflictParser.Parse(Text).Parts!);

        var update = AppReducer.Reduce(picked, new ConflictRecreated(tabId, Path, result));

        Assert.Equal(2, update.State.Shell.Conflict.Editor!.UnresolvedCount);
        Assert.False(update.State.Shell.Conflict.Editor.CanUndo);
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenARecreatedFileTheEditorRefuses_WhenTheResultArrives_ThenTheEditorClosesWithTheReason()
    {
        var state = Opened(Text);
        var tabId = state.Shell.Conflict.Editor!.TabId;

        var update = AppReducer.Reduce(
            state,
            new ConflictRecreated(tabId, Path, ConflictReadResult.Unsupported("it is binary")));

        Assert.False(update.State.Shell.Conflict.IsOpen);
        var post = update.Effects.OfType<PostStatusEffect>().Single();
        Assert.Contains("it is binary", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOpenEditor_WhenItsTabIsClosed_ThenTheEditorClosesAndTheKeymapGoesBackToNormal()
    {
        var typing = Press(Opened(Text), ActionIds.ConflictEditorEdit).State;
        var tabId = typing.Shell.Conflict.Editor!.TabId;

        var update = AppReducer.Reduce(typing, new CloseTabCommand(tabId));

        Assert.False(update.State.Shell.Conflict.IsOpen);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenALongFile_WhenMovingTheCursorDown_ThenThePanesScrollWithTheViewportOfTheMode()
    {
        var narrow = new ConflictViewports(new(3, 2), new(3, 2), new(3, 2), new(3, 2));
        var state = Opened(Text);

        var update = AppReducer.Reduce(state, new ConflictActionPressed(ActionIds.ListDown, 6, narrow));

        Assert.True(update.State.Shell.Conflict.Editor!.TopScroll > 0);
        Assert.Equal(narrow, update.State.Shell.Conflict.Viewports);
    }

    [Fact]
    public void GivenAHalfPageAction_WhenPressed_ThenTheCursorMovesByHalfTheRowsOfTheActivePane()
    {
        var viewports = new ConflictViewports(new(8, 2), new(8, 2), new(8, 2), new(8, 2));
        var state = Opened(Text);

        var update = AppReducer.Reduce(state, new ConflictActionPressed(ActionIds.ListHalfPageDown, null, viewports));
        var expected = AppReducer.Reduce(state, new ConflictActionPressed(ActionIds.ListDown, 4, viewports));

        Assert.Equal(expected.State.Shell.Conflict.Editor!.Row, update.State.Shell.Conflict.Editor!.Row);
        Assert.NotEqual(state.Shell.Conflict.Editor!.Row, update.State.Shell.Conflict.Editor.Row);
    }

    [Fact]
    public void GivenADialogOverTheEditor_WhenAnActionArrives_ThenTheEditorLeavesItAlone()
    {
        var state = Opened(Text);
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(new ConfirmPrompt("t", ["l"], "c", "go"), new ConflictTabClosed(), null)).State;

        var update = AppReducer.Reduce(asked, new ConflictActionPressed(ActionIds.ConflictEditorOurs, null, Tall));

        Assert.Equal(ConflictPick.None, update.State.Shell.Conflict.Editor!.SelectedPick);
    }

    private static AppUpdate Press(AppState state, ActionId action) =>
        AppReducer.Reduce(state, new ConflictActionPressed(action, null, Tall));

    private static ConflictFileRead Read(AppState state, string text)
    {
        var tab = state.Workspace.ActiveTab!;
        var generation = state.Shell.Conflict.Generation;

        return new ConflictFileRead(
            tab.Id,
            generation,
            Path,
            ConflictKind.BothModified,
            ConflictReadResult.OfText(ConflictParser.Parse(text).Parts!));
    }

    private static AppUpdate Reading() =>
        AppReducer.Reduce(Loaded(Entry(Path, ConflictKind.BothModified)), new ConflictOpenRequested(true, Tall));

    // The editor open on a file, reached through the real chain: the request, then the result of its read.
    private static AppState Opened(string text)
    {
        var reading = Reading();

        return AppReducer.Reduce(reading.State, Read(reading.State, text)).State;
    }

    private static AppState Loaded(params StatusEntry[] entries)
    {
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Location())).State;
        var tabId = state.Workspace.ActiveTabId!.Value;
        var head = new HeadState(HeadKind.Branch, "main", null);
        var snapshot = new StatusSnapshot(new StatusBranch(head, null, null, null), entries, []);
        state = Send(state, tabId, new WorkingTreeLoadedCommand(snapshot));
        state = Send(state, tabId, new FocusPaneCommand(PaneKind.WorkingUnstaged));

        return entries.Length == 0
            ? state
            : Send(state, tabId, new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 0));
    }

    private static AppState Send(AppState state, TabId tabId, RepositoryCommand command) =>
        AppReducer.Reduce(state, new TabCommand(tabId, command)).State;

    private static StatusEntry Entry(string path, ConflictKind conflict) =>
        new(
            StatusEntryKind.Unmerged,
            path,
            null,
            FileChangeKind.Unmodified,
            FileChangeKind.Modified,
            null,
            conflict,
            null);

    private static RepositoryLocation Location()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitunia-conflict-reducer-tests", "repo");
        var gitDirectory = System.IO.Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
