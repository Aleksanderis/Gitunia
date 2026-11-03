using System.Collections.Immutable;
using System.Globalization;
using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Search;

public class GraphSearchReducerTests
{
    [Fact]
    public void GivenOnlyTheCaretMoves_WhenEditing_ThenTheScanAndGenerationStay()
    {
        var typed = Editing("fix");

        var update = GraphSearchReducer.Reduce(typed, new EditSearchTextCommand("fix", 1));

        Assert.Equal(1, update.State.Caret);
        Assert.Equal(typed.Generation, update.State.Generation);
        Assert.Empty(update.Effects);
        Assert.StartsWith("/f|ix", GraphSearchStatusText.Describe(update.State, -1, 0), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTextInsertedInTheMiddle_WhenEditing_ThenTheQueryIsRescannedAndTheCaretFollows()
    {
        var update = GraphSearchReducer.Reduce(Editing("fx"), new EditSearchTextCommand("fix", 2));

        Assert.Equal(new SearchQuery(SearchField.Message, "fix"), update.State.Query);
        Assert.Equal(2, update.State.Caret);
        Assert.Single(update.Effects);
    }    private static GraphSearchState Apply(GraphSearchState state, params GraphSearchCommand[] commands) =>
        commands.Aggregate(state, (current, command) => GraphSearchReducer.Reduce(current, command).State);

    private static GraphSearchState Editing(string text) => Apply(GraphSearchState.Initial, new OpenSearchCommand(), new EditSearchTextCommand(text));

    private static ScanProgressCommand Progress(GraphSearchState state, int next, int loaded, bool complete, params int[] matches) =>
        new(state.Generation, [.. matches], next, loaded, complete);

    private static GraphSearchState Scanned(string text, int loaded, params int[] matches)
    {
        var state = Editing(text);
        return Apply(state, Progress(state, loaded, loaded, true, matches));
    }

    private static IReadOnlyList<GraphSearchEffect> Step(GraphSearchState state, bool forward, int count, int selected) =>
        GraphSearchReducer.Reduce(state, new StepMatchCommand(forward, count, selected)).Effects;

    [Fact]
    public void GivenIdleSearch_WhenOpening_ThenTheBoxIsOpenAndEmpty()
    {
        var update = GraphSearchReducer.Reduce(GraphSearchState.Initial, new OpenSearchCommand());

        Assert.Equal(SearchPhase.Editing, update.State.Phase);
        Assert.Equal(string.Empty, update.State.Text);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenOpenBox_WhenTyping_ThenAScanForTheNewGenerationIsRequested()
    {
        var opened = Apply(GraphSearchState.Initial, new OpenSearchCommand());

        var update = GraphSearchReducer.Reduce(opened, new EditSearchTextCommand("fix"));

        Assert.Equal(new SearchQuery(SearchField.Message, "fix"), update.State.Query);
        Assert.True(update.State.IsScanning);
        Assert.False(update.State.IsComplete);
        Assert.Equal([new ScanSearchEffect(update.State.Generation)], update.Effects);
        Assert.True(update.State.Generation > opened.Generation);
    }

    [Fact]
    public void GivenOnlyAPrefixTyped_WhenEditing_ThenNothingIsScanned()
    {
        var update = GraphSearchReducer.Reduce(Editing("fix"), new EditSearchTextCommand("author:"));

        Assert.Null(update.State.Query);
        Assert.Empty(update.Effects);
        Assert.True(update.State.IsComplete);
    }

    [Fact]
    public void GivenAScanInFlight_WhenTheQueryChanges_ThenTheOldResultIsDropped()
    {
        var first = Editing("fix");
        var second = Apply(first, new EditSearchTextCommand("fixe"));

        var late = GraphSearchReducer.Reduce(second, Progress(first, 100, 100, true, 1, 2));

        Assert.Empty(late.State.Matches);
        Assert.Empty(late.Effects);
        Assert.Equal(0, late.State.Scanned);
    }

    [Fact]
    public void GivenMoreRowsToScan_WhenAProgressArrives_ThenItAccumulatesAndAsksForTheNextSlice()
    {
        var state = Editing("x");

        var update = GraphSearchReducer.Reduce(state, Progress(state, 5000, 12000, false, 3, 4999));

        Assert.Equal([3, 4999], update.State.Matches);
        Assert.Equal(5000, update.State.Scanned);
        Assert.True(update.State.IsScanning);
        Assert.Equal([new ScanSearchEffect(state.Generation)], update.Effects);
    }

    [Fact]
    public void GivenEverythingScannedAndLoaded_WhenAProgressArrives_ThenTheSearchIsComplete()
    {
        var state = Scanned("x", 10, 2);

        Assert.True(state.IsComplete);
        Assert.False(state.IsScanning);
    }

    [Fact]
    public void GivenAllLoadedRowsScannedButLoadingContinues_WhenProgressArrives_ThenItIsNotComplete()
    {
        var state = Editing("x");

        var next = Apply(state, Progress(state, 10, 10, false, 2));

        Assert.False(next.IsComplete);
        Assert.False(next.IsScanning);
    }

    [Fact]
    public void GivenIdleScanAndNewRows_WhenTheGraphGrows_ThenScanningResumes()
    {
        var state = Editing("x");
        state = Apply(state, Progress(state, 10, 10, false));

        var update = GraphSearchReducer.Reduce(state, new GraphGrewCommand(25, false));

        Assert.True(update.State.IsScanning);
        Assert.Equal([new ScanSearchEffect(state.Generation)], update.Effects);
    }

    [Fact]
    public void GivenScanningInFlight_WhenTheGraphGrows_ThenNoSecondScanIsStarted()
    {
        var state = Editing("x");

        var update = GraphSearchReducer.Reduce(state, new GraphGrewCommand(25, false));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAllRowsScanned_WhenLoadingFinishes_ThenTheSearchCompletes()
    {
        var state = Editing("x");
        state = Apply(state, Progress(state, 10, 10, false, 4));

        var update = GraphSearchReducer.Reduce(state, new GraphGrewCommand(10, true));

        Assert.True(update.State.IsComplete);
    }

    [Fact]
    public void GivenMatches_WhenAccepting_ThenTheFirstMatchAtOrAfterTheSelectionIsRevealed()
    {
        var state = Scanned("x", 100, 5, 20, 40);

        var update = GraphSearchReducer.Reduce(state, new AcceptSearchCommand(20));

        Assert.Equal(SearchPhase.Active, update.State.Phase);
        Assert.Equal([new RevealMatchEffect(20)], update.Effects);
    }

    [Fact]
    public void GivenNoMatchAfterTheSelection_WhenAccepting_ThenTheSearchWrapsToTheFirst()
    {
        var state = Scanned("x", 100, 5, 20);

        var update = GraphSearchReducer.Reduce(state, new AcceptSearchCommand(50));

        Assert.Equal([new RevealMatchEffect(5)], update.Effects);
        Assert.Equal(GraphSearchReducer.WrappedNotice, update.State.Notice);
    }

    [Fact]
    public void GivenNoMatchesAtAll_WhenAccepting_ThenNoMatchIsReported()
    {
        var state = Scanned("x", 100);

        var update = GraphSearchReducer.Reduce(state, new AcceptSearchCommand(0));

        Assert.Empty(update.Effects);
        Assert.Equal(GraphSearchReducer.NoMatchNotice, update.State.Notice);
    }

    [Fact]
    public void GivenAScanStillRunningWithoutMatches_WhenAcceptingAndTheFirstMatchArrives_ThenItIsRevealed()
    {
        var accepted = Apply(Editing("x"), new AcceptSearchCommand(10));
        Assert.True(accepted.JumpPending);

        var update = GraphSearchReducer.Reduce(accepted, Progress(accepted, 5000, 20000, false, 4, 700));

        Assert.Equal([new RevealMatchEffect(700), new ScanSearchEffect(accepted.Generation)], update.Effects);
        Assert.False(update.State.JumpPending);
    }

    [Fact]
    public void GivenAScanThatFindsNothing_WhenAcceptedAndTheScanCompletes_ThenNoMatchIsReported()
    {
        var accepted = Apply(Editing("x"), new AcceptSearchCommand(0));

        var update = GraphSearchReducer.Reduce(accepted, Progress(accepted, 50, 50, true));

        Assert.False(update.State.JumpPending);
        Assert.Equal(GraphSearchReducer.NoMatchNotice, update.State.Notice);
    }

    [Fact]
    public void GivenEmptyQuery_WhenAccepting_ThenTheSearchCloses()
    {
        var state = Apply(GraphSearchState.Initial, new OpenSearchCommand());

        var update = GraphSearchReducer.Reduce(state, new AcceptSearchCommand(3));

        Assert.Equal(SearchPhase.Idle, update.State.Phase);
    }

    [Fact]
    public void GivenActiveSearch_WhenSteppingForward_ThenTheNextMatchAfterTheSelectionIsRevealed()
    {
        var active = Apply(Scanned("x", 100, 5, 20, 40), new AcceptSearchCommand(0));

        Assert.Equal([new RevealMatchEffect(20)], Step(active, true, 1, 5));
        Assert.Equal([new RevealMatchEffect(40)], Step(active, true, 2, 5));
        Assert.Equal([new RevealMatchEffect(20)], Step(active, true, 1, 7));
    }

    [Fact]
    public void GivenSelectionOnTheLastMatch_WhenSteppingForward_ThenItWraps()
    {
        var active = Apply(Scanned("x", 100, 5, 20, 40), new AcceptSearchCommand(0));

        var update = GraphSearchReducer.Reduce(active, new StepMatchCommand(true, 1, 40));

        Assert.Equal([new RevealMatchEffect(5)], update.Effects);
        Assert.Equal(GraphSearchReducer.WrappedNotice, update.State.Notice);
    }

    [Fact]
    public void GivenActiveSearch_WhenSteppingBackward_ThenThePreviousMatchIsRevealedAndItWraps()
    {
        var active = Apply(Scanned("x", 100, 5, 20, 40), new AcceptSearchCommand(0));

        Assert.Equal([new RevealMatchEffect(20)], Step(active, false, 1, 40));
        Assert.Equal([new RevealMatchEffect(5)], Step(active, false, 2, 40));
        var wrapped = GraphSearchReducer.Reduce(active, new StepMatchCommand(false, 1, 5));
        Assert.Equal([new RevealMatchEffect(40)], wrapped.Effects);
        Assert.Equal(GraphSearchReducer.WrappedNotice, wrapped.State.Notice);
    }

    [Fact]
    public void GivenIncompleteScanWithNoLaterMatch_WhenSteppingForward_ThenTheJumpWaitsForTheScan()
    {
        var editing = Editing("x");
        var active = Apply(editing, Progress(editing, 100, 5000, false, 5), new AcceptSearchCommand(0));

        var update = GraphSearchReducer.Reduce(active, new StepMatchCommand(true, 1, 5));

        Assert.True(update.State.JumpPending);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenNoSearch_WhenStepping_ThenANoticeSaysSo()
    {
        var update = GraphSearchReducer.Reduce(GraphSearchState.Initial, new StepMatchCommand(true, 1, 0));

        Assert.Equal(GraphSearchReducer.NoSearchNotice, update.State.Notice);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenOpenSearch_WhenCancelling_ThenEverythingIsClearedAndPendingScansAreStale()
    {
        var editing = Editing("x");

        var update = GraphSearchReducer.Reduce(editing, new CancelSearchCommand());

        Assert.False(update.State.IsOpen);
        Assert.Null(update.State.Query);
        Assert.True(update.State.Generation > editing.Generation);
        Assert.Empty(GraphSearchReducer.Reduce(update.State, Progress(editing, 10, 10, true, 1)).State.Matches);
    }

    [Fact]
    public void GivenAcceptedSearch_WhenTheTextIsEdited_ThenItIsIgnored()
    {
        var active = Apply(Scanned("x", 10, 1), new AcceptSearchCommand(0));

        var update = GraphSearchReducer.Reduce(active, new EditSearchTextCommand("y"));

        Assert.Equal(active, update.State);
    }

    [Fact]
    public void GivenMatches_WhenDescribing_ThenTheStatusShowsPositionAndProgress()
    {
        var editing = Editing("fix");
        var state = Apply(editing, Progress(editing, 50, 200, false, 10, 30));

        Assert.Equal("/fix_  2/2+ searching 25%", GraphSearchStatusText.Describe(state, 30, 200));
        Assert.Equal("/fix_  2+ found searching 25%", GraphSearchStatusText.Describe(state, 11, 200));
        Assert.Equal(string.Empty, GraphSearchStatusText.Describe(GraphSearchState.Initial, 0, 0));
    }

    [Fact]
    public void GivenAcceptedCompleteSearch_WhenDescribing_ThenThereIsNoCursorAndNoProgress()
    {
        var state = Apply(Scanned("fix", 10, 4), new AcceptSearchCommand(0));

        Assert.Equal("/fix  1/1", GraphSearchStatusText.Describe(state, 4, 10));
    }

    [Fact]
    public void GivenHundredThousandMatches_WhenSteppingForward_ThenTheNextOneIsFound()
    {
        var matches = Enumerable.Range(0, 100_000).Select(index => index * 2).ToImmutableArray();
        var state = GraphSearchState.Initial with { Phase = SearchPhase.Active, Query = new SearchQuery(SearchField.Message, "x"), Matches = matches, IsComplete = true };

        Assert.Equal([new RevealMatchEffect(100_000)], Step(state, true, 1, 99_999));
    }

    [Fact]
    public void GivenScannedSearch_WhenTheGraphIsReplaced_ThenMatchesResetAndANewScanStarts()
    {
        var state = Scanned("fix", 10, 2, 5);

        var update = GraphSearchReducer.Reduce(state, new GraphReplacedCommand(12));

        Assert.Empty(update.State.Matches);
        Assert.Equal(0, update.State.Scanned);
        Assert.False(update.State.IsComplete);
        Assert.Equal(state.Generation + 1, update.State.Generation);
        Assert.Equal(update.State.Generation, Assert.IsType<ScanSearchEffect>(Assert.Single(update.Effects)).Generation);
    }

    [Fact]
    public void GivenNoQuery_WhenTheGraphIsReplaced_ThenNothingChanges()
    {
        var update = GraphSearchReducer.Reduce(GraphSearchState.Initial, new GraphReplacedCommand(3));

        Assert.Equal(GraphSearchState.Initial, update.State);
        Assert.Empty(update.Effects);
    }

    private static ObjectId IdOf(int number)
    {
        Assert.True(ObjectId.TryParse(number.ToString("x40", CultureInfo.InvariantCulture), out var id));
        return id;
    }

    [Fact]
    public void GivenBodyPrefix_WhenTyping_ThenGitIsAskedAndNoRowIsScannedYet()
    {
        var opened = Apply(GraphSearchState.Initial, new OpenSearchCommand());

        var update = GraphSearchReducer.Reduce(opened, new EditSearchTextCommand("body:closes"));

        Assert.True(update.State.Query!.IsAwaitingBodyHits);
        Assert.True(update.State.IsScanning);
        Assert.Equal(new SearchBodiesEffect(update.State.Generation, "closes"), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenBodyQuery_WhenTheAnswerArrives_ThenTheHitsAreKeptAndTheScanStarts()
    {
        var state = Editing("body:closes");
        var hits = new HashSet<ObjectId> { IdOf(2) };

        var update = GraphSearchReducer.Reduce(state, new BodyHitsCommand(state.Generation, hits, string.Empty));

        Assert.False(update.State.Query!.IsAwaitingBodyHits);
        Assert.Same(hits, update.State.Query.BodyHits);
        Assert.Equal(new ScanSearchEffect(state.Generation), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenBodyQuery_WhenAnAnswerOfAnOlderGenerationArrives_ThenItIsIgnored()
    {
        var older = Editing("body:clo");
        var newer = Apply(older, new EditSearchTextCommand("body:closes"));

        var stale = new BodyHitsCommand(older.Generation, new HashSet<ObjectId>(), string.Empty);

        var update = GraphSearchReducer.Reduce(newer, stale);

        Assert.Equal(newer, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenBodyQuery_WhenGitFails_ThenTheSearchEndsWithANoticeAndNoMatches()
    {
        var state = Editing("body:closes");

        var update = GraphSearchReducer.Reduce(state, new BodyHitsCommand(state.Generation, null, "boom"));

        Assert.True(update.State.IsComplete);
        Assert.False(update.State.IsScanning);
        Assert.Equal($"{GraphSearchReducer.BodySearchFailedNotice}: boom", update.State.Notice);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAwaitedBodyQuery_WhenRowsAreAppended_ThenNothingIsScanned()
    {
        var state = Editing("body:closes");

        var update = GraphSearchReducer.Reduce(state, new GraphGrewCommand(50, false));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnsweredBodyQuery_WhenTheGraphIsReplaced_ThenGitIsAskedAgain()
    {
        var typed = Editing("body:closes");
        var state = Apply(typed, new BodyHitsCommand(typed.Generation, new HashSet<ObjectId>(), string.Empty));

        var update = GraphSearchReducer.Reduce(state, new GraphReplacedCommand(10));

        Assert.True(update.State.Query!.IsAwaitingBodyHits);
        Assert.Equal(new SearchBodiesEffect(update.State.Generation, "closes"), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenAwaitedBodyQuery_WhenAccepting_ThenTheJumpWaitsForTheAnswer()
    {
        var state = Editing("body:closes");

        var update = GraphSearchReducer.Reduce(state, new AcceptSearchCommand(0));

        Assert.True(update.State.JumpPending);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAwaitedBodyQuery_WhenDescribing_ThenTheStatusSaysMessagesAreSearched()
    {
        Assert.Equal("/body:x_  0+ found searching messages", GraphSearchStatusText.Describe(Editing("body:x"), 0, 10));
    }
}
