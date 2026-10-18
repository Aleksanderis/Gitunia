using Gitunia.Core.Graph;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Rendering.Graph;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

public class SearchControllerTests
{
    private static CommitGraph Graph() => CommitFactory.Graph(
        CommitFactory.Commit(6, "Fix parser crash", [5]),
        CommitFactory.Commit(5, "Add search box", [4]),
        CommitFactory.Commit(4, "Fix typo in readme", [3]),
        CommitFactory.Commit(3, "Refactor loader", [2]),
        CommitFactory.Commit(2, "fix lint", [1]),
        CommitFactory.Commit(1, "Initial commit", []));

    [Fact]
    public void GivenGraph_WhenTypingASearchAndAccepting_ThenTheFirstMatchAtTheSelectionIsSelectedAndModeIsNormal()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 1 };

        harness.Press(new Key('/'));
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("fix");
        Assert.Equal("/fix_  3 found", harness.StatusLine);
        harness.Press(Key.Enter);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(2, harness.SelectedRow);
        Assert.Equal(new SearchQuery(SearchField.Message, "fix"), harness.Session.Source.Highlight);
    }

    [Fact]
    public void GivenAcceptedSearch_WhenPressingNAndShiftN_ThenSelectionStepsThroughMatchesAndWraps()
    {
        using var harness = new PromptHarness(Graph());
        harness.Press(new Key('/'));
        harness.Type("fix");
        harness.Press(Key.Enter);
        Assert.Equal(0, harness.SelectedRow);

        harness.Press(new Key('n'));
        Assert.Equal(2, harness.SelectedRow);
        harness.Press(new Key('n'));
        Assert.Equal(4, harness.SelectedRow);
        harness.Press(new Key('n'));
        Assert.Equal(0, harness.SelectedRow);
        harness.Press(new Key('N'));
        Assert.Equal(4, harness.SelectedRow);
    }

    [Fact]
    public void GivenPrefixes_WhenSearching_ThenAuthorAndHashAreMatched()
    {
        using var harness = new PromptHarness(Graph());

        harness.Press(new Key('/'));
        harness.Type("hash:0000003");
        harness.Press(Key.Enter);

        Assert.Equal(3, harness.SelectedRow);
    }

    [Fact]
    public void GivenTypedText_WhenBackspacing_ThenTheQueryShrinksAndRescans()
    {
        using var harness = new PromptHarness(Graph());
        harness.Press(new Key('/'));
        harness.Type("fixx");
        Assert.Contains("0 found", harness.StatusLine, StringComparison.Ordinal);

        harness.Press(Key.Backspace);

        Assert.StartsWith("/fix_", harness.StatusLine, StringComparison.Ordinal);
        Assert.Equal("/fix_  1/3", harness.StatusLine);
    }

    [Fact]
    public void GivenOpenSearchBox_WhenPressingEscape_ThenItClosesWithoutMovingAndClearsTheHighlight()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 3 };
        harness.Press(new Key('/'));
        harness.Type("fix");

        harness.Press(Key.Esc);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(3, harness.SelectedRow);
        Assert.Null(harness.Session.Source.Highlight);
        Assert.Equal(string.Empty, harness.StatusLine);
    }

    [Fact]
    public void GivenAcceptedSearch_WhenPressingEscapeInNormalMode_ThenTheHighlightIsCleared()
    {
        using var harness = new PromptHarness(Graph());
        harness.Press(new Key('/'));
        harness.Type("fix");
        harness.Press(Key.Enter);

        harness.Press(Key.Esc);

        Assert.Null(harness.Session.Source.Highlight);
        Assert.False(harness.Session.Search.IsOpen);
    }

    [Fact]
    public void GivenNoSearch_WhenPressingN_ThenANoticeExplainsAndNothingMoves()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 2 };

        harness.Press(new Key('n'));

        Assert.Equal(GraphSearchReducer.NoSearchNotice, harness.Notice);
        Assert.Equal(2, harness.SelectedRow);
    }

    [Fact]
    public void GivenSearchBoxOpen_WhenTypingChordKeys_ThenTheyAreTextAndNothingFires()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 1 };
        harness.Press(new Key('/'));

        harness.Type("gg q@");

        Assert.Equal(1, harness.SelectedRow);
        Assert.Equal("/gg q@_", harness.StatusLine.Split("  ")[0]);
    }

    [Fact]
    public void GivenSearchOverAStillLoadingLog_WhenTheMatchArrivesLater_ThenTheJumpHappensThen()
    {
        var first = CommitFactory.Graph(CommitFactory.Commit(3, "alpha", [2]), CommitFactory.Commit(2, "beta", [1]), CommitFactory.Commit(1, "gamma", []));
        using var harness = new PromptHarness(first, isLoading: true);
        harness.Press(new Key('/'));
        harness.Type("zeta");
        harness.Press(Key.Enter);
        Assert.True(harness.Session.Search.JumpPending);
        Assert.Contains("0+ found", harness.StatusLine, StringComparison.Ordinal);

        var more = CommitFactory.Graph(CommitFactory.Commit(9, "the zeta commit", [8]), CommitFactory.Commit(8, "later", []));
        harness.Session.Append([more[0], more[1]]);

        Assert.Equal(3, harness.SelectedRow);
        Assert.False(harness.Session.Search.JumpPending);

        harness.Session.Complete();
        Assert.True(harness.Session.Search.IsComplete);
    }

    [Fact]
    public void GivenAcceptedSearch_WhenTheGraphIsReplacedByARefresh_ThenTheSearchRescansTheNewRows()
    {
        using var harness = new PromptHarness(Graph());
        harness.Press(new Key('/'));
        harness.Type("fix");
        harness.Press(Key.Enter);
        Assert.Equal(3, harness.Session.Search.Matches.Length);
        var refreshed = CommitFactory.Graph(
            CommitFactory.Commit(7, "Fix flaky test", [6]),
            CommitFactory.Commit(6, "Fix parser crash", [5]),
            CommitFactory.Commit(5, "Add search box", [4]),
            CommitFactory.Commit(4, "Unrelated", []));

        harness.Session.ReplaceGraph([.. Enumerable.Range(0, refreshed.Count).Select(index => refreshed[index])]);

        Assert.Equal(2, harness.Session.Search.Matches.Length);
        Assert.Equal(4, harness.Session.Search.Scanned);
        Assert.True(harness.Session.Search.IsComplete);
        Assert.Equal(harness.Session.GraphVersion, harness.Session.SearchGraphVersion);
        Assert.Equal(0, harness.SelectedRow);
    }

    [Fact]
    public void GivenLargeGraph_WhenSearching_ThenSlicesRunUntilEveryRowWasScanned()
    {
        var commits = Enumerable.Range(1, 12_000)
            .Select(number => CommitFactory.Commit(number, number % 4000 == 0 ? $"needle {number}" : $"hay {number}", number == 1 ? [] : [number - 1]))
            .Reverse()
            .ToArray();
        using var harness = new PromptHarness(CommitFactory.Graph(commits));

        harness.Press(new Key('/'));
        harness.Type("needle");

        Assert.Equal(12_000, harness.Session.Search.Scanned);
        Assert.Equal(3, harness.Session.Search.Matches.Length);
        Assert.True(harness.Session.Search.IsComplete);
    }

    [Fact]
    public void GivenDeferredWork_WhenTypingBetweenSlices_ThenInputIsHandledAndTheStaleScanStops()
    {
        var commits = Enumerable.Range(1, 12_000)
            .Select(number => CommitFactory.Commit(number, $"hay {number}", number == 1 ? [] : [number - 1]))
            .Reverse()
            .ToArray();
        var queue = new Queue<Action>();
        using var harness = new PromptHarness(CommitFactory.Graph(commits), deferred: queue);

        harness.Press(new Key('/'));
        harness.Type("hay");
        Assert.Equal(0, harness.Session.Search.Scanned);
        while (queue.Count > 0)
        {
            queue.Dequeue()();
        }

        // Only the slices of the latest query ran; older generations were dropped.
        Assert.Equal(12_000, harness.Session.Search.Scanned);

        harness.Type("x");
        queue.Dequeue()();
        Assert.True(harness.Session.Search.Scanned is > 0 and < 12_000);
        harness.Press(Key.Backspace);
        while (queue.Count > 0)
        {
            queue.Dequeue()();
        }

        Assert.Equal("hay", harness.Session.Search.Text);
        Assert.Equal(12_000, harness.Session.Search.Scanned);
        Assert.Equal(12_000, harness.Session.Search.Matches.Length);
    }

    [Fact]
    public void GivenActiveSearch_WhenOpeningGrAndPressingEscape_ThenOnlyTheRefPromptCloses()
    {
        using var harness = new PromptHarness(Graph());
        harness.Press(new Key('/'));
        harness.Type("fix");
        harness.Press(Key.Enter);

        harness.Press(new Key(' '));
        harness.Press(new Key('f'));
        harness.Press(Key.Esc);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.True(harness.Session.Search.IsOpen);
        Assert.StartsWith("/fix", harness.StatusLine, StringComparison.Ordinal);
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void WaitUntil(PromptHarness harness, Func<PromptHarness, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition(harness) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(5);
        }

        Assert.True(condition(harness), "Timed out waiting for the message search");
    }

    [Fact]
    public void GivenBodyQuery_WhenGitAnswers_ThenOnlyCommitsWithTheTextInTheirMessageMatchAndTheJumpFollows()
    {
        var searcher = new FakeCommitMessageSearcher();
        searcher.Hits.Add(CommitFactory.Id(3));
        searcher.Hits.Add(CommitFactory.Id(1));
        using var harness = new PromptHarness(Graph(), messages: searcher);

        harness.Press(new Key('/'));
        harness.Type("body:closes #4");
        WaitUntil(harness, h => h.Session.Search is { IsScanning: false, Query.IsAwaitingBodyHits: false });
        Assert.Equal("/body:closes #4_  2 found", harness.StatusLine);
        harness.Press(Key.Enter);

        Assert.Equal("closes #4", searcher.LastText);
        Assert.Equal(3, harness.SelectedRow);
        harness.Press(new Key('n'));
        Assert.Equal(5, harness.SelectedRow);
        harness.Press(new Key('n'));
        Assert.Equal(3, harness.SelectedRow);
    }

    [Fact]
    public void GivenSlowGit_WhenAcceptingBeforeTheAnswer_ThenTheJumpHappensWhenTheAnswerArrives()
    {
        var searcher = new FakeCommitMessageSearcher { Gate = NewGate() };
        searcher.Hits.Add(CommitFactory.Id(2));
        using var harness = new PromptHarness(Graph(), messages: searcher);
        harness.Press(new Key('/'));
        harness.Type("body:x");

        Assert.Contains("searching messages", harness.StatusLine, StringComparison.Ordinal);
        harness.Press(Key.Enter);
        Assert.Equal(0, harness.SelectedRow);
        searcher.Gate.SetResult();
        WaitUntil(harness, h => h.SelectedRow == 4);

        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public void GivenFailingGit_WhenSearchingBodies_ThenTheStatusExplainsAndNothingMatches()
    {
        var searcher = new FakeCommitMessageSearcher { Failure = new GitException("bad pattern") };
        using var harness = new PromptHarness(Graph(), messages: searcher);

        harness.Press(new Key('/'));
        harness.Type("body:x");
        WaitUntil(harness, h => h.Session.Search.IsComplete);

        Assert.Contains("Message search failed: git returned an error", harness.StatusLine, StringComparison.Ordinal);
        Assert.Contains("0 found", harness.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTextTypedAfterBodyPrefix_WhenTheAnswerOfTheOlderQueryArrives_ThenItIsDropped()
    {
        var searcher = new FakeCommitMessageSearcher { Gate = NewGate(), IgnoreCancellation = true };
        searcher.Hits.Add(CommitFactory.Id(2));
        using var harness = new PromptHarness(Graph(), messages: searcher);
        harness.Press(new Key('/'));
        harness.Type("body:x");

        harness.Press(Key.Backspace);
        harness.Press(Key.Backspace);
        harness.Press(Key.Backspace);
        harness.Press(Key.Backspace);
        harness.Press(Key.Backspace);
        harness.Press(Key.Backspace);
        harness.Type("fix");
        searcher.Gate.SetResult();
        await searcher.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(SearchField.Message, harness.Session.Search.Query!.Field);
        Assert.Equal("/fix_  1/3", harness.StatusLine);
    }

    [Fact]
    public void GivenNoSearcher_WhenSearchingBodies_ThenTheSearchEndsWithANotice()
    {
        using var harness = new PromptHarness(Graph());

        harness.Press(new Key('/'));
        harness.Type("body:x");

        Assert.Contains(GraphSearchReducer.BodySearchFailedNotice, harness.StatusLine, StringComparison.Ordinal);
    }
}
