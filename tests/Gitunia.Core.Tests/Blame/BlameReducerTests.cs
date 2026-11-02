using Gitunia.Core.Blame;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Blame;

// The blame view through the root reducer: state plus command gives state plus effects, nothing else.
public class BlameReducerTests
{
    private const int Page = 10;
    private const string Commit = "1111111111111111111111111111111111111111";

    [Fact]
    public void GivenAFileAtARevision_WhenOpening_ThenTheViewOpensAndTheReadIsRequestedForTheActiveTab()
    {
        var state = Opened("alpha", "beta");
        var active = state.Workspace.ActiveTabId!.Value;

        var update = AppReducer.Reduce(state, new BlameOpenRequested("src/a.txt", Commit));

        var start = Assert.IsType<StartBlameEffect>(Assert.Single(update.Effects));
        Assert.Equal(active, start.TabId);
        Assert.Equal("src/a.txt", start.Path);
        Assert.Equal(Commit, start.Revision);
        Assert.Equal(start.Generation, update.State.Shell.Blame.Generation);
        Assert.Equal("src/a.txt @ 1111111", update.State.Shell.Blame.View!.Title);
        Assert.True(update.State.Shell.HasEditor);
    }

    [Fact]
    public void GivenAWorkingTreeFile_WhenOpening_ThenTheTitleSaysWorkingTreeAndTheRevisionIsNull()
    {
        var update = AppReducer.Reduce(Opened("alpha"), new BlameOpenRequested("a.txt", null));

        Assert.Null(Assert.IsType<StartBlameEffect>(Assert.Single(update.Effects)).Revision);
        Assert.Equal("a.txt working tree", update.State.Shell.Blame.View!.Title);
    }

    [Fact]
    public void GivenNoTabOrAnOpenView_WhenOpening_ThenNothingHappens()
    {
        var none = AppReducer.Reduce(AppState.Empty, new BlameOpenRequested("a.txt", null));
        var open = Viewing(3);
        var again = AppReducer.Reduce(open, new BlameOpenRequested("b.txt", null));

        Assert.Empty(none.Effects);
        Assert.False(none.State.Shell.Blame.IsOpen);
        Assert.Empty(again.Effects);
        Assert.Equal("a.txt", again.State.Shell.Blame.View!.Path);
    }

    [Fact]
    public void GivenAnotherOverlayIsOpen_WhenOpening_ThenTheViewStaysClosed()
    {
        var state = Opened("alpha");
        state = state with { Shell = state.Shell with { Overlay = OverlayKind.Help } };

        var update = AppReducer.Reduce(state, new BlameOpenRequested("a.txt", null));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Shell.Blame.IsOpen);
    }

    [Fact]
    public void GivenARefusedOpen_WhenReducing_ThenTheWarningIsPosted()
    {
        var update = AppReducer.Reduce(Opened("alpha"), new BlameOpenRefused("Select a file to blame it"));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenTheLinesAreKnown_WhenLoaded_ThenTheCountIsStoredAndProgressUpdatesIt()
    {
        var state = Viewing(0);
        var generation = state.Shell.Blame.Generation;
        var tab = state.Shell.Blame.View!.TabId;

        var loaded = AppReducer.Reduce(state, new BlameLoaded(tab, generation, BlameContentKind.Text, 500)).State;
        var progressed = AppReducer.Reduce(loaded, new BlameProgressed(generation, 120)).State;
        var finished = AppReducer.Reduce(progressed, new BlameFinished(generation, 500)).State;

        Assert.Equal(500, loaded.Shell.Blame.View!.LineCount);
        Assert.Equal(120, progressed.Shell.Blame.View!.BlamedLines);
        Assert.False(progressed.Shell.Blame.View!.IsComplete);
        Assert.True(finished.Shell.Blame.View!.IsComplete);
        Assert.Equal(500, finished.Shell.Blame.View!.BlamedLines);
    }

    [Fact]
    public void GivenAResultOfAClosedRead_WhenItArrives_ThenItIsDropped()
    {
        var state = Viewing(5);
        var stale = state.Shell.Blame.Generation - 1;
        var tab = state.Shell.Blame.View!.TabId;

        var loaded = AppReducer.Reduce(state, new BlameLoaded(tab, stale, BlameContentKind.Text, 99)).State;
        var progressed = AppReducer.Reduce(state, new BlameProgressed(stale, 4)).State;
        var failed = AppReducer.Reduce(state, new BlameFailed(stale, "late")).State;

        Assert.Equal(5, loaded.Shell.Blame.View!.LineCount);
        Assert.Equal(0, progressed.Shell.Blame.View!.BlamedLines);
        Assert.True(failed.Shell.Blame.IsOpen);
    }

    [Theory]
    [InlineData(BlameContentKind.Binary, "binary file")]
    [InlineData(BlameContentKind.Missing, "not in the working tree")]
    public void GivenAFileThatCannotBeBlamed_WhenLoaded_ThenTheViewClosesAndTheStatusSaysWhy(
        BlameContentKind kind,
        string reason)
    {
        var state = Viewing(0);
        var tab = state.Shell.Blame.View!.TabId;

        var update = AppReducer.Reduce(state, new BlameLoaded(tab, state.Shell.Blame.Generation, kind, 0));

        Assert.False(update.State.Shell.Blame.IsOpen);
        Assert.Contains(update.Effects, effect => effect is StopBlameEffect);
        var post = Assert.IsType<PostStatusEffect>(update.Effects.Single(effect => effect is PostStatusEffect));
        Assert.Contains(reason, post.Text);
    }

    [Fact]
    public void GivenTheReadFails_WhenTheFailureArrives_ThenTheViewClosesWithAnErrorStatus()
    {
        var state = Viewing(0);

        var update = AppReducer.Reduce(state, new BlameFailed(state.Shell.Blame.Generation, "boom"));

        Assert.False(update.State.Shell.Blame.IsOpen);
        var post = Assert.IsType<PostStatusEffect>(update.Effects.Single(effect => effect is PostStatusEffect));
        Assert.Equal(StatusSeverity.Error, post.Severity);
    }

    [Fact]
    public void GivenTheTabIsClosedBeforeTheLinesArrive_WhenLoaded_ThenTheViewClosesQuietly()
    {
        var state = Viewing(0);
        var tab = state.Shell.Blame.View!.TabId;
        var closed = AppReducer.Reduce(state, new CloseTabCommand(tab));

        var update = AppReducer.Reduce(closed.State, new BlameLoaded(tab, state.Shell.Blame.Generation, BlameContentKind.Text, 3));

        Assert.False(closed.State.Shell.Blame.IsOpen);
        Assert.Contains(closed.Effects, effect => effect is StopBlameEffect);
        Assert.DoesNotContain(update.Effects, effect => effect is PostStatusEffect);
    }

    [Fact]
    public void GivenMotionKeys_WhenPressed_ThenTheCursorMovesWithinTheLines()
    {
        var state = Viewing(100);

        state = Press(state, ActionIds.ListDown, count: 5);
        Assert.Equal(5, Cursor(state));

        state = Press(state, ActionIds.ListUp);
        Assert.Equal(4, Cursor(state));

        state = Press(state, ActionIds.ListBottom);
        Assert.Equal(99, Cursor(state));

        state = Press(state, ActionIds.ListDown);
        Assert.Equal(99, Cursor(state));

        state = Press(state, ActionIds.ListTop);
        Assert.Equal(0, Cursor(state));
    }

    [Fact]
    public void GivenAClickOnALine_WhenReducing_ThenTheCursorMovesThereClamped()
    {
        var state = Viewing(10);

        Assert.Equal(7, Cursor(AppReducer.Reduce(state, new BlameRowClicked(7)).State));
        Assert.Equal(9, Cursor(AppReducer.Reduce(state, new BlameRowClicked(70)).State));
    }

    [Fact]
    public void GivenALineWithACommit_WhenShowingIt_ThenTheViewClosesAndTheGraphIsAskedForTheCommit()
    {
        var state = Viewing(10);
        var tab = state.Shell.Blame.View!.TabId;

        var update = AppReducer.Reduce(state, Pressed(ActionIds.BlameShowCommit, Commit));

        Assert.False(update.State.Shell.Blame.IsOpen);
        Assert.IsType<StopBlameEffect>(update.Effects[0]);
        var locate = Assert.IsType<LocateGraphCommitEffect>(update.Effects[1]);
        Assert.Equal(tab, locate.TabId);
        Assert.Equal(Commit, locate.Commit.ToString());
        Assert.Equal("1111111", locate.Label);
    }

    [Theory]
    [InlineData(null, BlameReducer.NoCommitYetNotice)]
    [InlineData(BlameCommit.UncommittedId, BlameReducer.UncommittedNotice)]
    public void GivenALineWithoutACommitYet_WhenShowingIt_ThenTheViewStaysOpenAndSaysWhy(string? commit, string notice)
    {
        var state = Viewing(10);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.BlameShowCommit, commit));

        Assert.True(update.State.Shell.Blame.IsOpen);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(notice, post.Text);
    }

    [Fact]
    public void GivenTheView_WhenPressingBackOrQuit_ThenItClosesAndStopsTheRead()
    {
        foreach (var action in new[] { ActionIds.NavigateBack, ActionIds.Cancel, ActionIds.AppQuit })
        {
            var state = Viewing(10);
            var generation = state.Shell.Blame.Generation;

            var update = AppReducer.Reduce(state, Pressed(action, null));

            Assert.False(update.State.Shell.Blame.IsOpen);
            Assert.Equal(generation + 1, update.State.Shell.Blame.Generation);
            Assert.Equal(new StopBlameEffect(generation), Assert.Single(update.Effects));
        }
    }

    [Fact]
    public void GivenTheView_WhenAnUnrelatedActionIsPressed_ThenNothingBehindItMoves()
    {
        var state = Viewing(10);

        var update = AppReducer.Reduce(state, Pressed(ActionIds.BranchDelete, null));

        Assert.Empty(update.Effects);
        Assert.Equal(state.Shell.Blame, update.State.Shell.Blame);
    }

    [Fact]
    public void GivenTheSearchKey_WhenTypingAndAccepting_ThenInsertModeIsAskedAndTheFindEffectNamesTheTextAndStart()
    {
        var state = Press(Viewing(50), ActionIds.ListDown, count: 3);

        var opened = AppReducer.Reduce(state, Pressed(ActionIds.SearchOpen, null));
        Assert.Equal(new SetKeyModeEffect(KeyMode.Insert), Assert.Single(opened.Effects));
        var typed = AppReducer.Reduce(opened.State, new BlameKeyTyped(new KeyEvent("x"))).State;
        typed = AppReducer.Reduce(typed, new BlameKeyTyped(new KeyEvent("y"))).State;
        var accepted = AppReducer.Reduce(typed, Pressed(ActionIds.InputAccept, null));

        Assert.Equal("xy", typed.Shell.Blame.View!.SearchText);
        Assert.Contains(new SetKeyModeEffect(KeyMode.Normal), accepted.Effects);
        var find = Assert.IsType<FindBlameMatchEffect>(accepted.Effects.Single(effect => effect is FindBlameMatchEffect));
        Assert.Equal(("xy", 2, true), (find.Query, find.Start, find.Forward));
        Assert.False(accepted.State.Shell.Blame.View!.IsEditingSearch);
    }

    [Fact]
    public void GivenAnAcceptedSearch_WhenSteppingWithNAndShiftN_ThenTheFindEffectGoesFromTheCursorInThatDirection()
    {
        var state = Searched(Viewing(50), "foo");

        var next = AppReducer.Reduce(state, Pressed(ActionIds.SearchNext, null, count: 2));
        var previous = AppReducer.Reduce(state, Pressed(ActionIds.SearchPrevious, null));

        var forward = Assert.IsType<FindBlameMatchEffect>(Assert.Single(next.Effects));
        Assert.Equal(("foo", 0, true, 2), (forward.Query, forward.Start, forward.Forward, forward.Count));
        var backward = Assert.IsType<FindBlameMatchEffect>(Assert.Single(previous.Effects));
        Assert.False(backward.Forward);
    }

    [Fact]
    public void GivenNoSearchWasTyped_WhenSteppingAMatch_ThenAWarningSaysToPressSlash()
    {
        var update = AppReducer.Reduce(Viewing(5), Pressed(ActionIds.SearchNext, null));

        Assert.Equal(BlameReducer.NoSearchNotice, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text);
    }

    [Fact]
    public void GivenTheResultOfASearch_WhenItArrives_ThenTheCursorJumpsOrAWarningSaysNoMatch()
    {
        var state = Searched(Viewing(50), "foo");
        var generation = state.Shell.Blame.Generation;

        var found = AppReducer.Reduce(state, new BlameMatchFound(generation, "foo", 31));
        var none = AppReducer.Reduce(state, new BlameMatchFound(generation, "foo", -1));
        var stale = AppReducer.Reduce(state, new BlameMatchFound(generation, "other", 31));

        Assert.Equal(31, Cursor(found.State));
        Assert.Contains("No match for foo", Assert.IsType<PostStatusEffect>(Assert.Single(none.Effects)).Text);
        Assert.Equal(0, Cursor(stale.State));
    }

    [Fact]
    public void GivenTheSearchBox_WhenPressingEscape_ThenTheSearchIsClearedAndTheViewStaysOpen()
    {
        var typing = AppReducer.Reduce(Viewing(5), Pressed(ActionIds.SearchOpen, null)).State;

        var update = AppReducer.Reduce(typing, Pressed(ActionIds.Cancel, null));

        Assert.True(update.State.Shell.Blame.IsOpen);
        Assert.False(update.State.Shell.Blame.View!.HasSearch);
        Assert.Equal(new SetKeyModeEffect(KeyMode.Normal), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenAKeptSearch_WhenPressingEscapeOnce_ThenOnlyTheSearchEndsAndTheNextEscapeCloses()
    {
        var state = Searched(Viewing(5), "foo");

        var first = AppReducer.Reduce(state, Pressed(ActionIds.Cancel, null));
        var second = AppReducer.Reduce(first.State, Pressed(ActionIds.Cancel, null));

        Assert.True(first.State.Shell.Blame.IsOpen);
        Assert.False(second.State.Shell.Blame.IsOpen);
    }

    [Fact]
    public void GivenTheSearchBoxIsOpen_WhenTheViewCloses_ThenKeyModeReturnsToNormal()
    {
        var typing = AppReducer.Reduce(Viewing(5), Pressed(ActionIds.SearchOpen, null)).State;

        var update = AppReducer.Reduce(typing, Pressed(ActionIds.AppQuit, null));

        Assert.Contains(new SetKeyModeEffect(KeyMode.Normal), update.Effects);
        Assert.False(update.State.Shell.Blame.IsOpen);
    }

    [Fact]
    public void GivenTheViewTabIsClosed_WhenAnActionArrives_ThenTheViewClosesInstead()
    {
        var state = Viewing(5);
        var tab = state.Shell.Blame.View!.TabId;
        var withTwo = AppReducer.Reduce(state, new OpenRepositoryCommand(Location("second"))).State;
        var closed = AppReducer.Reduce(withTwo, new CloseTabCommand(tab)).State;

        Assert.False(closed.Shell.Blame.IsOpen);
    }

    [Fact]
    public void GivenACommittedLine_WhenBlamingAtTheParent_ThenTheViewReadsTheParentRevisionAndKeepsAWayBack()
    {
        var state = Viewing(5);
        state = Press(state, ActionIds.ListDown);
        var before = state.Shell.Blame;

        var update = AppReducer.Reduce(state, ParentPressed(Commit, "old/a.txt", 7));

        var start = Assert.IsType<StartBlameEffect>(Assert.Single(update.Effects));
        var view = update.State.Shell.Blame.View!;
        Assert.Equal($"{Commit}^", start.Revision);
        Assert.Equal("old/a.txt", start.Path);
        Assert.Equal(before.Generation + 1, start.Generation);
        Assert.Equal(start.Generation, update.State.Shell.Blame.Generation);
        Assert.Equal("old/a.txt @ 1111111^", view.Title);
        Assert.Equal(0, view.LineCount);
        Assert.Equal(7, view.Cursor);
        Assert.Equal(new BlameStep("a.txt", null, 1), Assert.Single(view.Steps));
    }

    [Fact]
    public void GivenAParentBlameLoaded_WhenItsLinesAreFewerThanTheOriginalLine_ThenTheCursorIsClamped()
    {
        var state = AppReducer.Reduce(Viewing(5), ParentPressed(Commit, "a.txt", 40)).State;
        var tab = state.Shell.Blame.View!.TabId;

        state = AppReducer.Reduce(state, new BlameLoaded(tab, state.Shell.Blame.Generation, BlameContentKind.Text, 3))
            .State;

        Assert.Equal(2, Cursor(state));
    }

    [Fact]
    public void GivenAParentBlame_WhenGoingBack_ThenEachEscStepsOneBlameBackThenTheViewCloses()
    {
        var first = Press(Viewing(5), ActionIds.ListDown);
        var second = AppReducer.Reduce(first, ParentPressed(Commit, "a.txt", 3)).State;
        var third = AppReducer.Reduce(second, ParentPressed("2222222222222222222222222222222222222222", "a.txt", 2)).State;

        var back = AppReducer.Reduce(third, Pressed(ActionIds.NavigateBack, null));
        var backAgain = AppReducer.Reduce(back.State, Pressed(ActionIds.NavigateBack, null));
        var closed = AppReducer.Reduce(backAgain.State, Pressed(ActionIds.NavigateBack, null));

        Assert.Equal($"{Commit}^", Assert.IsType<StartBlameEffect>(Assert.Single(back.Effects)).Revision);
        Assert.Equal(3, back.State.Shell.Blame.View!.Cursor);
        Assert.Null(Assert.IsType<StartBlameEffect>(Assert.Single(backAgain.Effects)).Revision);
        Assert.Equal(1, backAgain.State.Shell.Blame.View!.Cursor);
        Assert.Empty(backAgain.State.Shell.Blame.View!.Steps);
        Assert.False(closed.State.Shell.Blame.IsOpen);
    }

    [Fact]
    public void GivenUncommittedPendingAndRootLines_WhenBlamingAtTheParent_ThenTheViewStaysAndSaysWhy()
    {
        var state = Viewing(5);
        var generation = state.Shell.Blame.Generation;

        var uncommitted = AppReducer.Reduce(state, ParentPressed(BlameCommit.UncommittedId, "a.txt", 0));
        var pending = AppReducer.Reduce(state, ParentPressed(null, null, 0));
        var root = AppReducer.Reduce(state, ParentPressed(Commit, "a.txt", 0, isBoundary: true));

        Assert.Equal(BlameReducer.UncommittedNotice, Assert.IsType<PostStatusEffect>(Assert.Single(uncommitted.Effects)).Text);
        Assert.Equal(BlameReducer.NoCommitYetNotice, Assert.IsType<PostStatusEffect>(Assert.Single(pending.Effects)).Text);
        Assert.Equal(BlameReducer.NoParentNotice, Assert.IsType<PostStatusEffect>(Assert.Single(root.Effects)).Text);
        Assert.All([uncommitted, pending, root], update => Assert.Equal(generation, update.State.Shell.Blame.Generation));
        Assert.All([uncommitted, pending, root], update => Assert.Empty(update.State.Shell.Blame.View!.Steps));
    }

    [Fact]
    public void GivenAParentBlameThatFails_WhenTheFailureArrives_ThenTheViewGoesBackAndWarns()
    {
        var state = AppReducer.Reduce(Press(Viewing(5), ActionIds.ListDown), ParentPressed(Commit, "a.txt", 3)).State;
        var generation = state.Shell.Blame.Generation;

        var update = AppReducer.Reduce(state, new BlameFailed(generation, "Could not blame a.txt: not found"));

        Assert.True(update.State.Shell.Blame.IsOpen);
        Assert.Empty(update.State.Shell.Blame.View!.Steps);
        Assert.Equal(1, update.State.Shell.Blame.View!.Cursor);
        Assert.Contains(update.Effects, effect => effect is StartBlameEffect { Revision: null });
        Assert.Contains(update.Effects, effect => effect is PostStatusEffect { Text: "Could not blame a.txt: not found" });
    }

    [Fact]
    public void GivenTheToggles_WhenPressed_ThenTheBlameRunsAgainWithTheSwitchAndTheCursorAndStackStay()
    {
        var state = Press(Viewing(5), ActionIds.ListDown);

        var whitespace = AppReducer.Reduce(state, Pressed(ActionIds.BlameIgnoreWhitespace, null));
        var moves = AppReducer.Reduce(whitespace.State, Pressed(ActionIds.BlameDetectMoves, null));
        var copies = AppReducer.Reduce(moves.State, Pressed(ActionIds.BlameDetectCopies, null));
        var off = AppReducer.Reduce(copies.State, Pressed(ActionIds.BlameIgnoreWhitespace, null));

        Assert.Equal(new BlameOptions(IgnoreWhitespace: true), Assert.IsType<StartBlameEffect>(Assert.Single(whitespace.Effects)).Options);
        Assert.Equal(new BlameOptions(true, true), Assert.IsType<StartBlameEffect>(Assert.Single(moves.Effects)).Options);
        Assert.Equal(new BlameOptions(true, true, true), Assert.IsType<StartBlameEffect>(Assert.Single(copies.Effects)).Options);
        Assert.Equal(new BlameOptions(false, true, true), Assert.IsType<StartBlameEffect>(Assert.Single(off.Effects)).Options);
        Assert.Equal(1, off.State.Shell.Blame.View!.Cursor);
        Assert.Empty(off.State.Shell.Blame.View!.Steps);
        Assert.Equal(state.Shell.Blame.Generation + 4, off.State.Shell.Blame.Generation);
    }

    [Fact]
    public void GivenOptionsOnAParentBlame_WhenGoingBack_ThenTheOptionsStay()
    {
        var state = AppReducer.Reduce(Viewing(5), Pressed(ActionIds.BlameDetectMoves, null)).State;
        state = AppReducer.Reduce(state, ParentPressed(Commit, "a.txt", 0)).State;

        var back = AppReducer.Reduce(state, Pressed(ActionIds.NavigateBack, null));

        Assert.True(Assert.IsType<StartBlameEffect>(Assert.Single(back.Effects)).Options!.DetectMoves);
    }

    [Fact]
    public void GivenALoadedResultWithAnIgnoreRevsFile_WhenReduced_ThenTheViewKnowsTheFileIsInUse()
    {
        var opened = AppReducer.Reduce(Opened("alpha"), new BlameOpenRequested("a.txt", null)).State;
        var tab = opened.Shell.Blame.View!.TabId;

        var loaded = AppReducer.Reduce(
            opened,
            new BlameLoaded(tab, opened.Shell.Blame.Generation, BlameContentKind.Text, 2, ".git-blame-ignore-revs")).State;

        Assert.Equal(".git-blame-ignore-revs", loaded.Shell.Blame.View!.IgnoreRevsFile);
    }

    [Fact]
    public void GivenAnOpenSearchBox_WhenTogglingAnOption_ThenTheKeyIsTextAndNothingRuns()
    {
        var state = AppReducer.Reduce(Viewing(5), Pressed(ActionIds.SearchOpen, null)).State;

        var update = AppReducer.Reduce(state, Pressed(ActionIds.BlameDetectMoves, null));

        Assert.Empty(update.Effects);
        Assert.Equal(state.Shell.Blame.Generation, update.State.Shell.Blame.Generation);
    }

    private static BlameActionPressed ParentPressed(
        string? commit,
        string? path,
        int originalLine,
        bool isBoundary = false) =>
        new(ActionIds.BlameParent, null, Page, commit, path, originalLine, isBoundary);

    private static AppState Viewing(int lines)
    {
        var opened = AppReducer.Reduce(Opened("alpha"), new BlameOpenRequested("a.txt", null)).State;
        var tab = opened.Shell.Blame.View!.TabId;

        return AppReducer.Reduce(opened, new BlameLoaded(tab, opened.Shell.Blame.Generation, BlameContentKind.Text, lines))
            .State;
    }

    private static AppState Searched(AppState state, string text)
    {
        state = AppReducer.Reduce(state, Pressed(ActionIds.SearchOpen, null)).State;
        foreach (var character in text)
        {
            state = AppReducer.Reduce(state, new BlameKeyTyped(new KeyEvent(character.ToString()))).State;
        }

        return AppReducer.Reduce(state, Pressed(ActionIds.InputAccept, null)).State;
    }

    private static AppState Press(AppState state, ActionId action, int? count = null) =>
        AppReducer.Reduce(state, Pressed(action, null, count)).State;

    private static BlameActionPressed Pressed(ActionId action, string? commit, int? count = null) =>
        new(action, count, Page, commit);

    private static int Cursor(AppState state) => state.Shell.Blame.View!.Cursor;

    private static AppState Opened(string name) =>
        AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Location(name))).State;

    private static AppState Opened(string first, string second) =>
        AppReducer.Reduce(Opened(first), new OpenRepositoryCommand(Location(second))).State;

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-blame-reducer-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
