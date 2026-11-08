using Gitunia.Core.Operations;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Rebase;
using Gitunia.Core.Running;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Rebase.RebaseTestData;

namespace Gitunia.Core.Tests.Rebase;

// The pure transitions of the interactive rebase editor: state + command -> state + effects, no git, no terminal.
public class RebaseEditorReducerTests
{
    private const string First = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Second = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Third = "cccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void GivenACommitInTheGraph_WhenOpening_ThenTheRangeIsReadForThatCommitAndTheSessionWaits()
    {
        var update = Reduce(RebaseEditorSession.Empty, Open(new RebaseSelection(null, null, Target)));

        var read = Assert.IsType<ReadRebaseRangeEffect>(Assert.Single(update.Effects));
        Assert.Equal(Target, read.TargetHash);
        Assert.Equal(Target[..7], read.Label);
        Assert.Equal("main", read.BranchName);
        Assert.Equal(Tip, read.OldTip);
        Assert.Equal(1, read.Generation);
        Assert.True(update.Session.IsReading);
        Assert.Equal(1, update.Session.Generation);
    }

    [Fact]
    public void GivenASidebarBranch_WhenOpening_ThenTheLabelIsTheBranchName()
    {
        var chosen = new RebaseSelection(SidebarRowKind.LocalBranch, "develop", Target);

        var update = Reduce(RebaseEditorSession.Empty, Open(chosen));

        Assert.Equal("develop", Assert.IsType<ReadRebaseRangeEffect>(Assert.Single(update.Effects)).Label);
    }

    [Fact]
    public void GivenNoSelection_WhenOpeningTheRootEditor_ThenTheWholeHistoryIsRead()
    {
        var update = Reduce(RebaseEditorSession.Empty, Open(new RebaseSelection(null, null, null), isRoot: true));

        var read = Assert.IsType<ReadRebaseRangeEffect>(Assert.Single(update.Effects));
        Assert.Null(read.TargetHash);
        Assert.Equal(RebaseEditorReducer.RootLabel, read.Label);
    }

    [Fact]
    public void GivenNoSelectionTarget_WhenOpening_ThenTheStatusAsksForOneAndNothingIsRead()
    {
        var update = Reduce(RebaseEditorSession.Empty, Open(new RebaseSelection(null, null, null)));

        Assert.Contains("Select a commit", Warning(update.Effects), StringComparison.Ordinal);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenAStashRow_WhenOpening_ThenItIsNoTargetAndTheStatusAsksForOne()
    {
        var update = Reduce(RebaseEditorSession.Empty, Open(new RebaseSelection(SidebarRowKind.Stash, "s", Target)));

        Assert.Contains("Select a commit", Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTrackedChanges_WhenOpening_ThenNoGitReadsHappenAndTheStatusRefuses()
    {
        var context = Context(Dirty(), busy: false);

        var update = RebaseEditorReducer.Reduce(
            RebaseEditorSession.Empty,
            Open(new RebaseSelection(null, null, Target)),
            context);

        Assert.Contains("uncommitted", Warning(update.Effects), StringComparison.OrdinalIgnoreCase);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenATargetTheBranchIsAlreadyAt_WhenOpening_ThenNothingIsReadAndTheStatusSaysSo()
    {
        var update = Reduce(RebaseEditorSession.Empty, Open(new RebaseSelection(null, null, Tip)));

        Assert.Contains("nothing to rebase", Warning(update.Effects), StringComparison.Ordinal);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenUncommittedChanges_WhenOpeningTheRootEditor_ThenItIsRefusedLikeTheOtherRebase()
    {
        var update = RebaseEditorReducer.Reduce(
            RebaseEditorSession.Empty,
            Open(new RebaseSelection(null, null, null), isRoot: true),
            Context(Dirty(), busy: false));

        Assert.Contains("uncommitted", Warning(update.Effects), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GivenARunningRebase_WhenOpening_ThenTheStatusSaysBusyAndNothingIsRead()
    {
        var update = RebaseEditorReducer.Reduce(
            RebaseEditorSession.Empty,
            Open(new RebaseSelection(null, null, Target)),
            Context(Repository(), busy: true));

        Assert.Contains("Busy", Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnotherOverlay_WhenOpening_ThenNothingHappens()
    {
        var update = RebaseEditorReducer.Reduce(
            RebaseEditorSession.Empty,
            Open(new RebaseSelection(null, null, Target)),
            Context(Repository(), overlay: true));

        Assert.Empty(update.Effects);
        Assert.Equal(RebaseEditorSession.Empty, update.Session);
    }

    [Fact]
    public void GivenARangeBeingRead_WhenOpeningAgain_ThenNothingHappens()
    {
        var reading = RebaseEditorSession.Empty with { IsReading = true, Generation = 1 };

        var update = Reduce(reading, Open(new RebaseSelection(null, null, Target)));

        Assert.Empty(update.Effects);
        Assert.Equal(reading, update.Session);
    }

    [Fact]
    public void GivenTheEditorOpen_WhenOpeningAgain_ThenNothingHappens()
    {
        var open = Opened();

        var update = Reduce(open, Open(new RebaseSelection(null, null, Target)));

        Assert.Empty(update.Effects);
        Assert.Equal(open, update.Session);
    }

    [Fact]
    public void GivenTheRangeRead_WhenItArrives_ThenTheEditorOpensOnTheTodoAndTheReadEnds()
    {
        var todo = Todo();

        var update = Reduce(Reading(), new RebaseEditorRangeRead(Tab, 1, todo, Target));

        Assert.Same(todo, update.Session.Editor);
        Assert.False(update.Session.IsReading);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheEditorClosedMeanwhile_WhenTheRangeArrives_ThenItIsDroppedAndTheReadEnds()
    {
        var closed = Reading() with { Generation = 2 };

        var update = Reduce(closed, new RebaseEditorRangeRead(Tab, 1, Todo(), Target));

        Assert.Null(update.Session.Editor);
        Assert.False(update.Session.IsReading);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenHeadMovedWhileReading_WhenTheRangeArrives_ThenNothingOpensAndTheStatusSaysSo()
    {
        var moved = Repository(OnBranch("main", Target));

        var update = RebaseEditorReducer.Reduce(
            Reading(),
            new RebaseEditorRangeRead(Tab, 1, Todo(), Older),
            Context(moved));

        Assert.Contains("changed while the todo was open", Warning(update.Effects), StringComparison.Ordinal);
        Assert.Null(update.Session.Editor);
    }

    [Fact]
    public void GivenADialogOpenedMeanwhile_WhenTheRangeArrives_ThenItDoesNotOpenOverIt()
    {
        var update = RebaseEditorReducer.Reduce(
            Reading(),
            new RebaseEditorRangeRead(Tab, 1, Todo(), Target),
            Context(overlay: true));

        Assert.Null(update.Session.Editor);
        Assert.Equal(RebaseEditorReducer.ChangedNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
    }

    [Fact]
    public void GivenATargetTheBranchAlreadyHas_WhenTheEmptyRangeArrives_ThenNoEditorOpensAndTheStatusSaysSo()
    {
        var empty = RebaseEditorState.Create(Tab, "main", "x", RebaseBase.OntoCommit(Target), Tip, []);

        var update = Reduce(Reading(), new RebaseEditorRangeRead(Tab, 1, empty, Target));

        Assert.Equal(RebaseEditorReducer.EmptyRangeNotice, Warning(update.Effects));
        Assert.Null(update.Session.Editor);
    }

    [Fact]
    public void GivenARangeThatCannotBeRead_WhenTheFailureArrives_ThenTheTextIsPostedAndTheReadEnds()
    {
        var update = Reduce(Reading(), new RebaseEditorReadFailed(Tab, "boom", StatusSeverity.Error));

        var post = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(("boom", StatusSeverity.Error), (post.Text, post.Severity));
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenACancelledRead_WhenItArrives_ThenTheReadEndsQuietly()
    {
        var update = Reduce(Reading(), new RebaseEditorReadCancelled(Tab));

        Assert.False(update.Session.IsReading);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheEditorOpen_WhenAnyOtherActionArrives_ThenItIsConsumedAndNothingChanges()
    {
        var open = Opened();

        var update = Reduce(open, Press(ActionIds.RebaseInteractive));

        Assert.Equal(open, update.Session);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenActions_WhenPressed_ThenTheHighlightedCommitGetsThatAction()
    {
        var session = Opened();

        session = Reduce(session, Press(ActionIds.RebaseEditorSquash)).Session;
        Assert.Equal(RebaseTodoAction.Squash, session.Editor!.Rows[0].Action);
        session = Reduce(session, Press(ActionIds.RebaseEditorFixup)).Session;
        Assert.Equal(RebaseTodoAction.Fixup, session.Editor!.Rows[0].Action);
        session = Reduce(session, Press(ActionIds.RebaseEditorDrop)).Session;
        Assert.Equal(RebaseTodoAction.Drop, session.Editor!.Rows[0].Action);
        session = Reduce(session, Press(ActionIds.RebaseEditorPick)).Session;
        Assert.Equal(RebaseTodoAction.Pick, session.Editor!.Rows[0].Action);
    }

    [Fact]
    public void GivenACount_WhenMovingUp_ThenTheCommitMovesThatManyRows()
    {
        var session = Opened(highlighted: 2);

        var update = Reduce(session, Press(ActionIds.RebaseEditorMoveUp, count: 2));

        Assert.Equal([Third, First, Second], update.Session.Editor!.Rows.Select(row => row.Hash));
        Assert.Equal(0, update.Session.Editor.SelectedIndex);
    }

    [Fact]
    public void GivenACount_WhenMovingDown_ThenTheCommitMovesThatManyRows()
    {
        var update = Reduce(Opened(), Press(ActionIds.RebaseEditorMoveDown, count: 2));

        Assert.Equal([Second, Third, First], update.Session.Editor!.Rows.Select(row => row.Hash));
    }

    [Fact]
    public void GivenListMotions_WhenPressed_ThenTheHighlightMoves()
    {
        var session = Opened();

        session = Reduce(session, Press(ActionIds.ListDown)).Session;
        Assert.Equal(1, session.Editor!.SelectedIndex);
        session = Reduce(session, Press(ActionIds.ListBottom)).Session;
        Assert.Equal(2, session.Editor!.SelectedIndex);
        session = Reduce(session, Press(ActionIds.ListTop)).Session;
        Assert.Equal(0, session.Editor!.SelectedIndex);
    }

    [Fact]
    public void GivenAClickOnARow_WhenReducing_ThenTheRowIsHighlighted()
    {
        var update = Reduce(Opened(), new RebaseEditorRowClicked(Tab, 2));

        Assert.Equal(2, update.Session.Editor!.SelectedIndex);
    }

    [Fact]
    public void GivenAnUntouchedTodo_WhenPressingEsc_ThenTheEditorClosesAtOnce()
    {
        var update = Reduce(Opened(), Press(ActionIds.Cancel));

        Assert.Null(update.Session.Editor);
        Assert.Empty(update.Effects);
    }

    [Theory]
    [InlineData("navigate.back")]
    [InlineData("app.quit")]
    public void GivenAnEditedTodo_WhenPressingBackOrQuitTwice_ThenTheFirstAsksAndTheSecondCloses(string id)
    {
        var action = new ActionId(id);
        var edited = Reduce(Opened(), Press(ActionIds.RebaseEditorDrop)).Session;

        var asked = Reduce(edited, Press(action));
        var closed = Reduce(asked.Session, Press(action));

        Assert.True(asked.Session.Editor!.IsDiscardPending);
        Assert.Null(closed.Session.Editor);
    }

    [Fact]
    public void GivenAPendingDiscard_WhenMovingTheHighlight_ThenTheNextEscAsksAgain()
    {
        var edited = Reduce(Opened(), Press(ActionIds.RebaseEditorDrop)).Session;
        var asked = Reduce(edited, Press(ActionIds.Cancel)).Session;

        var moved = Reduce(asked, Press(ActionIds.ListDown)).Session;
        var again = Reduce(moved, Press(ActionIds.Cancel)).Session;

        Assert.False(moved.Editor!.IsDiscardPending);
        Assert.True(again.Editor!.IsDiscardPending);
    }

    [Fact]
    public void GivenACommitWithoutAnEditedMessage_WhenRewording_ThenItsFullMessageIsRead()
    {
        var update = Reduce(Opened(), Press(ActionIds.RebaseEditorReword));

        var read = Assert.IsType<ReadRebaseMessageEffect>(Assert.Single(update.Effects));
        Assert.Equal(First, read.Hash);
        Assert.Equal("one", read.Subject);
        Assert.True(update.Session.IsReading);
        Assert.Equal(1, read.Generation);
    }

    [Fact]
    public void GivenARowThatWasReworded_WhenRewordingAgain_ThenTheEditorOpensAtOnceWithTheEditedMessage()
    {
        var session = OpenedReworded("kept subject\n\nkept body");

        var update = Reduce(session, Press(ActionIds.RebaseEditorReword));

        Assert.Equal("kept subject\n\nkept body", update.Session.Editor!.Edit!.Editor.Text);
        Assert.Equal(KeyMode.Insert, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenAnUnparsableHash_WhenRewording_ThenTheEditorOpensWithTheSubject()
    {
        var odd = RebaseEditorState.Create(Tab, "main", "x", RebaseBase.Root, Tip, [new("not-a-hash", "odd subject")]);

        var update = Reduce(Session(odd), Press(ActionIds.RebaseEditorReword));

        Assert.Equal("odd subject", update.Session.Editor!.Edit!.Editor.Text);
        Assert.Equal(KeyMode.Insert, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenAMessageBeingRead_WhenRewordingAgain_ThenNothingHappens()
    {
        var reading = Opened() with { IsReading = true };

        var update = Reduce(reading, Press(ActionIds.RebaseEditorReword));

        Assert.Empty(update.Effects);
        Assert.Equal(reading, update.Session);
    }

    [Fact]
    public void GivenAMergeRow_WhenRewording_ThenNothingHappensAndTheKeyModeStays()
    {
        var merge = RebaseEditorState.Create(
            Tab,
            "main",
            "x",
            RebaseBase.Root,
            Tip,
            [new(First, "Merge x", RebaseTodoAction.Merge, "-C x")]);

        var update = Reduce(Session(merge), Press(ActionIds.RebaseEditorReword));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheMessageRead_WhenItArrives_ThenTheEditorOpensWithTheWholeMessageInInsertMode()
    {
        var reading = Opened() with { IsReading = true, Generation = 1 };

        var update = Reduce(reading, new RebaseEditorMessageRead(Tab, 1, First, "subject\n\nthe body"));

        Assert.Equal("subject\n\nthe body", update.Session.Editor!.Edit!.Editor.Text);
        Assert.Equal(KeyMode.Insert, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenAnotherHighlightedCommit_WhenTheMessageArrives_ThenItIsDropped()
    {
        var reading = Opened(highlighted: 1) with { IsReading = true, Generation = 1 };

        var update = Reduce(reading, new RebaseEditorMessageRead(Tab, 1, First, "text"));

        Assert.Null(update.Session.Editor!.Edit);
        Assert.Empty(update.Effects);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenTheEditorClosedMeanwhile_WhenTheMessageArrives_ThenItIsDropped()
    {
        var closed = RebaseEditorSession.Empty with { IsReading = true, Generation = 2 };

        var update = Reduce(closed, new RebaseEditorMessageRead(Tab, 1, First, "text"));

        Assert.Null(update.Session.Editor);
        Assert.False(update.Session.IsReading);
    }

    [Fact]
    public void GivenTheMessageEditor_WhenTypingEnterAndSaving_ThenTheCommitIsAWordedRewordAndNormalModeIsBack()
    {
        var session = Opened() with { Editor = Opened().Editor!.BeginEdit("one") };

        session = Reduce(session, new RebaseEditorKeyTyped(Tab, new KeyEvent("!"))).Session;
        session = Reduce(session, Press(ActionIds.RebaseEditorMessageNewLine)).Session;
        session = Reduce(session, new RebaseEditorKeyTyped(Tab, new KeyEvent("b"))).Session;
        var saved = Reduce(session, Press(ActionIds.RebaseEditorMessageSave));

        var row = saved.Session.Editor!.Rows[0];
        Assert.Equal(RebaseTodoAction.Reword, row.Action);
        Assert.Equal("one!\nb", row.Message);
        Assert.Null(saved.Session.Editor.Edit);
        Assert.Equal(KeyMode.Normal, Assert.Single(saved.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenATypedKey_WhenReducing_ThenTheEarlierSessionKeepsItsText()
    {
        var before = Opened() with { Editor = Opened().Editor!.BeginEdit("one") };

        var after = Reduce(before, new RebaseEditorKeyTyped(Tab, new KeyEvent("x"))).Session;

        Assert.Equal("one", before.Editor!.Edit!.Editor.Text);
        Assert.Equal("onex", after.Editor!.Edit!.Editor.Text);
    }

    [Fact]
    public void GivenAnInactiveTab_WhenAKeyIsTyped_ThenItIsNotTaken()
    {
        var before = Opened() with { Editor = Opened().Editor!.BeginEdit("one") };

        var update = RebaseEditorReducer.Reduce(
            before,
            new RebaseEditorKeyTyped(Tab, new KeyEvent("x")),
            new RebaseEditorContext(Repository(), false, false, false));

        Assert.Equal(before, update.Session);
    }

    [Fact]
    public void GivenABlankMessage_WhenSaving_ThenTheEditorStaysOpenWithAWarning()
    {
        var session = Opened() with { Editor = Opened().Editor!.BeginEdit("  ") };

        var update = Reduce(session, Press(ActionIds.RebaseEditorMessageSave));

        Assert.Equal(RebaseEditorReducer.BlankMessageNotice, Warning(update.Effects));
        Assert.NotNull(update.Session.Editor!.Edit);
        Assert.Equal(RebaseTodoAction.Pick, update.Session.Editor.Rows[0].Action);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("navigate.back")]
    public void GivenTheMessageEditor_WhenCancelling_ThenTheListIsBackUnchangedInNormalMode(string id)
    {
        var session = Opened() with { Editor = Opened().Editor!.BeginEdit("changed") };

        var update = Reduce(session, Press(new ActionId(id)));

        Assert.Null(update.Session.Editor!.Edit);
        Assert.Equal(RebaseTodoAction.Pick, update.Session.Editor.Rows[0].Action);
        Assert.Equal(KeyMode.Normal, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenTheMessageEditor_WhenAnotherActionArrives_ThenItIsConsumedAndNothingChanges()
    {
        var session = Opened() with { Editor = Opened().Editor!.BeginEdit("text") };

        var update = Reduce(session, Press(ActionIds.RebaseEditorDrop));

        Assert.Equal(session, update.Session);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheMessageEditorOpen_WhenTheEditorCloses_ThenNormalModeIsBack()
    {
        var session = Opened() with { Editor = Opened().Editor!.BeginEdit("text") };

        var update = RebaseEditorReducer.Closed(session);

        Assert.Null(update.Session.Editor);
        Assert.Equal(KeyMode.Normal, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenAFirstKeptSquash_WhenStarting_ThenNoConfirmOpensAndTheStatusNamesTheRule()
    {
        var session = Reduce(Opened(), Press(ActionIds.RebaseEditorSquash)).Session;

        var update = Reduce(session, Press(ActionIds.RebaseEditorStart));

        Assert.Contains("cannot be a squash or a fixup", Warning(update.Effects), StringComparison.Ordinal);
        Assert.NotNull(update.Session.Editor);
    }

    [Fact]
    public void GivenADroppedCommit_WhenStarting_ThenTheConfirmNamesItAndTheUndoAndNothingRunsYet()
    {
        var session = Reduce(Opened(), Press(ActionIds.RebaseEditorDrop)).Session;

        var update = Reduce(session, Press(ActionIds.RebaseEditorStart));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Interactive rebase", confirm.Prompt.Title);
        Assert.Contains("2 of 3 commits kept, 1 dropped", confirm.Prompt.Lines, StringComparer.Ordinal);
        Assert.Contains(confirm.Prompt.Lines, line => line.Contains("one", StringComparison.Ordinal));
        Assert.Contains(UndoNotes.Reflog, confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.DoesNotContain("git reset", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(RebaseEditorReducer.TooSmallNotice, confirm.UnavailableNotice);
        Assert.Equal(new RebaseEditorStartConfirmed(Tab, session.Editor!), confirm.OnAccepted);
        Assert.Equal(session, update.Session);
    }

    [Fact]
    public void GivenTheConfirmAnswered_WhenStarting_ThenTheEditorClosesAndTheTodoGoesToTheEngine()
    {
        var todo = Reduce(Opened(), Press(ActionIds.RebaseEditorDrop)).Session.Editor!;

        var update = Reduce(Opened(), new RebaseEditorStartConfirmed(Tab, todo));

        var run = Assert.IsType<RunInteractiveRebaseEffect>(Assert.Single(update.Effects));
        Assert.Equal(todo, run.State);
        Assert.Null(update.Session.Editor);
    }

    [Fact]
    public void GivenHeadMovedWhileTheTodoIsOpen_WhenConfirming_ThenNothingIsRebasedAndTheTodoStays()
    {
        var session = Opened();
        var moved = Repository(OnBranch("main", Older));

        var update = RebaseEditorReducer.Reduce(
            session,
            new RebaseEditorStartConfirmed(Tab, session.Editor!),
            Context(moved));

        Assert.Contains("changed while the todo was open", Warning(update.Effects), StringComparison.Ordinal);
        Assert.Equal(session, update.Session);
    }

    [Fact]
    public void GivenAStoppedOperation_WhenConfirming_ThenTheRefusalIsPostedAndTheTodoStays()
    {
        var session = Opened();
        var stopped = Repository(operation: Stopped(RepositoryOperation.Merge));

        var update = RebaseEditorReducer.Reduce(
            session,
            new RebaseEditorStartConfirmed(Tab, session.Editor!),
            Context(stopped));

        Assert.Contains("in progress", Warning(update.Effects), StringComparison.Ordinal);
        Assert.NotNull(update.Session.Editor);
    }

    [Fact]
    public void GivenARunningRebase_WhenConfirming_ThenNothingStartsAndTheTodoStays()
    {
        var session = Opened();

        var update = RebaseEditorReducer.Reduce(
            session,
            new RebaseEditorStartConfirmed(Tab, session.Editor!),
            Context(busy: true));

        Assert.Contains("nothing was rebased", Warning(update.Effects), StringComparison.Ordinal);
        Assert.NotNull(update.Session.Editor);
    }

    [Fact]
    public void GivenTheRebaseFinished_WhenReporting_ThenTheStatusNamesTheUndoAndTheTabIsRefreshed()
    {
        var result = new InteractiveRebaseResult(OperationOutcome.Completed, Target, Tip);

        var update = Reduce(RebaseEditorSession.Empty, new RebaseEditorFinished(Tab, Todo(), result));

        var post = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Info, post.Severity);
        Assert.DoesNotContain("git reset", post.Text, StringComparison.Ordinal);
        Assert.Equal($"Undo: git reset --keep {Tip} (or ORIG_HEAD, the reflog)", post.Detail);
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect refresh && refresh.TabId == Tab);
    }

    [Fact]
    public void GivenAConflictingRebase_WhenReporting_ThenTheWarningNamesTheKeys()
    {
        var result = new InteractiveRebaseResult(OperationOutcome.Conflicted, null, Tip);

        var update = Reduce(RebaseEditorSession.Empty, new RebaseEditorFinished(Tab, Todo(), result));

        var post = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.Contains(OperationKeys.Continue, post.Text, StringComparison.Ordinal);
        Assert.Contains(Tip[..7], post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheEngineCouldNotStart_WhenTheResultArrives_ThenTheErrorIsPostedAndTheTodoComesBack()
    {
        var todo = Todo();

        var notStarted = new RebaseEditorNotStarted(Tab, todo, "GIT_SEQUENCE_EDITOR is set");

        var update = Reduce(RebaseEditorSession.Empty, notStarted);

        var post = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.Same(todo, update.Session.Editor);
    }

    [Fact]
    public void GivenADialogOpen_WhenTheTodoComesBack_ThenItDoesNotOpenOverIt()
    {
        var update = RebaseEditorReducer.Reduce(
            RebaseEditorSession.Empty,
            new RebaseEditorNotStarted(Tab, Todo(), "no"),
            Context(overlay: true));

        Assert.Null(update.Session.Editor);
        Assert.Single(update.Effects.OfType<PostStatusEffect>());
    }

    [Fact]
    public void GivenEveryEditorEffect_WhenInspectingIt_ThenItIsAnOperationEffectOfKindRebase()
    {
        var location = Location();
        RebaseEditorEffect[] built =
        [
            new ReadRebaseRangeEffect(Tab, location, 1, null, "x", "main", Tip),
            new ReadRebaseMessageEffect(Tab, location, 1, First, default(ObjectId), "s"),
            new RunInteractiveRebaseEffect(Tab, location, Todo()),
        ];

        Assert.All(built, effect => Assert.Equal(RunningOperationKind.Rebase, effect.Kind));
    }

    private static RebaseEditorUpdate Reduce(RebaseEditorSession session, RebaseEditorCommand command) =>
        RebaseEditorReducer.Reduce(session, command, Context());

    private static RebaseEditorContext Context(RepositoryState? tab = null, bool busy = false, bool overlay = false) =>
        new(tab ?? Repository(), true, busy, overlay);

    private static RepositoryState Dirty() => Repository(entries: Modified("tracked.txt"));

    private static RebaseEditorOpenRequested Open(RebaseSelection selection, bool isRoot = false) =>
        new(Tab, selection, isRoot);

    private static RebaseEditorActionPressed Press(ActionId action, int? count = null) =>
        new(Tab, action, count, 10);

    private static RebaseEditorState Todo() => RebaseEditorState.Create(
        Tab,
        "main",
        "topic",
        RebaseBase.OntoCommit(Target),
        Tip,
        [new(First, "one"), new(Second, "two"), new(Third, "three")]);

    private static RebaseEditorSession Session(RebaseEditorState state) => RebaseEditorSession.Empty with
    {
        Editor = state,
    };

    private static RebaseEditorSession Opened(int highlighted = 0) =>
        Session(Todo().MoveTo(highlighted));

    private static RebaseEditorSession OpenedReworded(string message)
    {
        var edited = Todo().BeginEdit(message).SaveEdit();

        return Session(edited);
    }

    private static RebaseEditorSession Reading() =>
        RebaseEditorSession.Empty with { IsReading = true, Generation = 1 };
}
