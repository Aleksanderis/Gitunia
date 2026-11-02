using Gitunia.Core.Diff;
using Gitunia.Core.Effects;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.FileHistory;

// The file history overlay through the root reducer: state plus command gives state plus effects, nothing else.
public class FileHistoryReducerTests
{
    private const int Page = 10;
    private const string Revision = "HEAD";

    [Fact]
    public void GivenAFile_WhenOpening_ThenTheReadOfItsHistoryIsRequestedFromTheGivenRevision()
    {
        var state = Opened("alpha");
        var tab = state.Workspace.ActiveTabId!.Value;

        var update = AppReducer.Reduce(state, new FileHistoryOpenRequested(tab, "dir/a b.txt", "abc123"));

        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(update.Effects));
        Assert.Equal(tab, read.TabId);
        Assert.Equal("dir/a b.txt", read.Path);
        Assert.Equal("abc123", read.Revision);
        Assert.True(update.State.Shell.FileHistory.IsReading);
        Assert.False(update.State.Shell.FileHistory.IsOpen);
    }

    [Fact]
    public void GivenAReadInFlight_WhenOpeningAgain_ThenNoSecondReadStartsAndTheStatusSaysSo()
    {
        var tab = Opened("alpha");
        var id = tab.Workspace.ActiveTabId!.Value;
        var reading = AppReducer.Reduce(tab, new FileHistoryOpenRequested(id, "a.txt", Revision)).State;

        var update = AppReducer.Reduce(reading, new FileHistoryOpenRequested(id, "b.txt", Revision));

        var status = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(FileHistoryReducer.StillReadingNotice, status.Text);
    }

    [Fact]
    public void GivenAnUnknownTab_WhenOpening_ThenNothingHappens()
    {
        var update = AppReducer.Reduce(
            Opened("alpha"),
            new FileHistoryOpenRequested(new TabId(99), "a.txt", Revision));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Shell.FileHistory.IsReading);
    }

    [Fact]
    public void GivenTheResultOfTheRead_WhenItArrives_ThenTheHistoryOpensAndTheFirstCommitsDiffIsRequested()
    {
        var update = Open(Opened("alpha"), 3);

        var history = update.State.Shell.FileHistory.History!;
        Assert.Equal(3, history.Entries.Count);
        Assert.Equal(0, history.SelectedIndex);
        var diff = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(update.Effects));
        Assert.Equal(history.Entries[0], diff.Entry);
        Assert.False(update.State.Shell.FileHistory.IsReading);
    }

    [Fact]
    public void GivenAnEmptyHistory_WhenTheResultArrives_ThenItOpensEmptyAndNoDiffIsRead()
    {
        var update = Open(Opened("alpha"), 0);

        Assert.True(update.State.Shell.FileHistory.IsOpen);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAResultOfAnOlderRead_WhenItArrives_ThenItIsDropped()
    {
        var state = Opened("alpha");
        var id = state.Workspace.ActiveTabId!.Value;
        var reading = AppReducer.Reduce(state, new FileHistoryOpenRequested(id, "a.txt", Revision));
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(reading.Effects));

        var update = AppReducer.Reduce(
            reading.State,
            new FileHistoryLoaded(id, read.Generation - 1, "a.txt", Revision, Entries(2)));

        Assert.False(update.State.Shell.FileHistory.IsOpen);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClosedTab_WhenItsResultArrives_ThenTheHistoryDoesNotOpen()
    {
        var state = Opened("alpha");
        var id = state.Workspace.ActiveTabId!.Value;
        var reading = AppReducer.Reduce(state, new FileHistoryOpenRequested(id, "a.txt", Revision));
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(reading.Effects));
        var closed = AppReducer.Reduce(reading.State, new CloseTabCommand(id)).State;

        var update = AppReducer.Reduce(closed, new FileHistoryLoaded(id, read.Generation, "a.txt", Revision, Entries(2)));

        Assert.False(update.State.Shell.FileHistory.IsOpen);
        Assert.False(update.State.Shell.FileHistory.IsReading);
    }

    [Fact]
    public void GivenAnotherTabShownMeanwhile_WhenTheResultArrives_ThenTheHistoryDoesNotOpenOverIt()
    {
        var state = Opened("alpha", "beta");
        var first = state.Workspace.Tabs[0].Id;
        var shown = AppReducer.Reduce(state, new ActivateTabCommand(first)).State;
        var reading = AppReducer.Reduce(shown, new FileHistoryOpenRequested(first, "a.txt", Revision));
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(reading.Effects));
        var switched = AppReducer.Reduce(reading.State, new NextTabCommand()).State;

        var update = AppReducer.Reduce(
            switched,
            new FileHistoryLoaded(first, read.Generation, "a.txt", Revision, Entries(2)));

        Assert.False(update.State.Shell.FileHistory.IsOpen);
        Assert.False(update.State.Shell.FileHistory.IsReading);
    }

    [Fact]
    public void GivenTheDiffResult_WhenItArrives_ThenItIsShownForTheHighlightedCommit()
    {
        var opened = Open(Opened("alpha"), 2);
        var diff = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(opened.Effects));

        var update = AppReducer.Reduce(opened.State, new FileHistoryDiffLoaded(diff.Generation, Document(5)));

        Assert.Equal(5, update.State.Shell.FileHistory.History!.Diff!.Rows.Count);
    }

    [Fact]
    public void GivenTheCursorMoved_WhenTheOlderDiffArrives_ThenItIsDroppedAndTheNewOneIsRead()
    {
        var opened = Open(Opened("alpha"), 3);
        var first = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(opened.Effects));

        var moved = Press(opened.State, ActionIds.ListDown);
        var second = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(moved.Effects));
        var late = AppReducer.Reduce(moved.State, new FileHistoryDiffLoaded(first.Generation, Document(4)));

        Assert.Equal(1, moved.State.Shell.FileHistory.History!.SelectedIndex);
        Assert.Equal(moved.State.Shell.FileHistory.History!.Entries[1], second.Entry);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.Null(late.State.Shell.FileHistory.History!.Diff);
    }

    [Fact]
    public void GivenADiffThatFailed_WhenItsResultArrives_ThenTheFailureIsShownInTheViewNotAsAStatus()
    {
        var opened = Open(Opened("alpha"), 2);
        var diff = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(opened.Effects));

        var update = AppReducer.Reduce(opened.State, new FileHistoryDiffFailed(diff.Generation, "boom"));

        Assert.Equal("boom", update.State.Shell.FileHistory.History!.DiffFailure);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAListRead_WhenItFails_ThenTheStatusShowsTheErrorAndTheViewStaysClosed()
    {
        var state = Opened("alpha");
        var id = state.Workspace.ActiveTabId!.Value;
        var reading = AppReducer.Reduce(state, new FileHistoryOpenRequested(id, "a.txt", Revision)).State;

        var update = AppReducer.Reduce(reading, new FileHistoryReadFailed("no log"));

        var status = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Error, status.Severity);
        Assert.False(update.State.Shell.FileHistory.IsReading);
        Assert.False(update.State.Shell.FileHistory.IsOpen);
    }

    [Fact]
    public void GivenAnOpenHistory_WhenSelectingTheCommit_ThenTheJumpToTheGraphIsRequestedForTheHighlightedHash()
    {
        var opened = Open(Opened("alpha"), 3);
        var moved = Press(opened.State, ActionIds.ListDown);

        var update = Press(moved.State, ActionIds.FileHistorySelectCommit);

        var jump = Assert.IsType<FileHistoryJumpEffect>(Assert.Single(update.Effects));
        Assert.Equal(Hash(1), jump.Hash);
        Assert.Equal(update.State.Workspace.ActiveTabId, jump.TabId);
        Assert.True(update.State.Shell.FileHistory.IsOpen);
    }

    [Fact]
    public void GivenAnEmptyHistory_WhenSelectingTheCommit_ThenTheStatusSaysNothingIsSelected()
    {
        var opened = Open(Opened("alpha"), 0);

        var update = Press(opened.State, ActionIds.FileHistorySelectCommit);

        var status = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(FileHistoryReducer.NoCommitNotice, status.Text);
    }

    [Fact]
    public void GivenTheJumpAnswers_WhenDoneOrRefused_ThenDoneClosesAndRefusedKeepsTheHistory()
    {
        var opened = Open(Opened("alpha"), 2).State;

        var refused = AppReducer.Reduce(opened, new FileHistoryJumpRefused("not loaded"));
        var done = AppReducer.Reduce(opened, new FileHistoryJumpDone());

        Assert.True(refused.State.Shell.FileHistory.IsOpen);
        Assert.Equal("not loaded", Assert.IsType<PostStatusEffect>(Assert.Single(refused.Effects)).Text);
        Assert.False(done.State.Shell.FileHistory.IsOpen);
    }

    [Theory]
    [InlineData("navigate.back")]
    [InlineData("cancel")]
    [InlineData("app.quit")]
    public void GivenAnOpenHistory_WhenGoingBackCancellingOrQuitting_ThenItClosesAndLateResultsAreDropped(string id)
    {
        var opened = Open(Opened("alpha"), 2);
        var diff = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(opened.Effects));

        var closed = Press(opened.State, new ActionId(id));
        var late = AppReducer.Reduce(closed.State, new FileHistoryDiffLoaded(diff.Generation, Document(3)));

        Assert.False(closed.State.Shell.FileHistory.IsOpen);
        Assert.False(late.State.Shell.FileHistory.IsOpen);
    }

    [Fact]
    public void GivenAnOpenHistory_WhenItsTabCloses_ThenTheHistoryCloses()
    {
        var opened = Open(Opened("alpha"), 2).State;
        var id = opened.Shell.FileHistory.History!.TabId;

        var closed = AppReducer.Reduce(opened, new CloseTabCommand(id)).State;

        Assert.False(closed.Shell.FileHistory.IsOpen);
    }

    [Fact]
    public void GivenScrollKeys_WhenPressed_ThenTheDiffScrollsAndStaysInsideTheDocument()
    {
        var opened = Open(Opened("alpha"), 2);
        var diff = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(opened.Effects));
        var loaded = AppReducer.Reduce(opened.State, new FileHistoryDiffLoaded(diff.Generation, Document(30))).State;

        var down = Press(loaded, ActionIds.DiffScrollDown, count: 3).State;
        var half = Press(down, ActionIds.DiffScrollHalfPageDown).State;
        var past = Press(half, ActionIds.DiffScrollHalfPageDown, count: 50).State;
        var top = Press(past, ActionIds.DiffScrollHalfPageUp, count: 99).State;

        Assert.Equal(3, down.Shell.FileHistory.History!.DiffScroll);
        Assert.Equal(3 + (Page / 2), half.Shell.FileHistory.History!.DiffScroll);
        Assert.Equal(29, past.Shell.FileHistory.History!.DiffScroll);
        Assert.Equal(0, top.Shell.FileHistory.History!.DiffScroll);
    }

    [Fact]
    public void GivenAClickOnARow_WhenReducing_ThenThatCommitIsHighlightedAndItsDiffRead()
    {
        var opened = Open(Opened("alpha"), 4).State;

        var update = AppReducer.Reduce(opened, new FileHistoryEntryClicked(2));

        Assert.Equal(2, update.State.Shell.FileHistory.History!.SelectedIndex);
        var diff = Assert.IsType<ReadFileHistoryDiffEffect>(Assert.Single(update.Effects));
        Assert.Equal(Hash(2), diff.Entry.Hash);
    }

    [Fact]
    public void GivenARefresh_WhenTheNewListArrives_ThenTheHighlightStaysOnTheSameCommit()
    {
        var opened = Open(Opened("alpha"), 3).State;
        var onSecond = Press(opened, ActionIds.ListDown).State;
        var refreshing = Press(onSecond, ActionIds.RepositoryRefresh);
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(refreshing.Effects));
        var history = onSecond.Shell.FileHistory.History!;
        var fresh = new[] { Entry(9) }.Concat(history.Entries).ToList();

        var update = AppReducer.Reduce(
            refreshing.State,
            new FileHistoryLoaded(read.TabId, read.Generation, read.Path, read.Revision, fresh));

        Assert.Equal(Hash(1), update.State.Shell.FileHistory.History!.Selected!.Hash);
        Assert.Equal(2, update.State.Shell.FileHistory.History!.SelectedIndex);
    }

    [Fact]
    public void GivenEveryConcreteFileHistoryCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<FileHistoryCommand>();
        Assert.NotEmpty(commands);
        var context = new FileHistoryContext(null);

        foreach (var command in commands)
        {
            try
            {
                FileHistoryReducer.Reduce(FileHistorySession.Empty, command, context);
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in FileHistoryReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static AppUpdate Press(AppState state, ActionId action, int? count = null) =>
        AppReducer.Reduce(state, new FileHistoryActionPressed(action, count, Page));

    private static AppUpdate Open(AppState state, int count)
    {
        var id = state.Workspace.ActiveTabId!.Value;
        var reading = AppReducer.Reduce(state, new FileHistoryOpenRequested(id, "a.txt", Revision));
        var read = Assert.IsType<ReadFileHistoryEffect>(Assert.Single(reading.Effects));

        return AppReducer.Reduce(
            reading.State,
            new FileHistoryLoaded(read.TabId, read.Generation, read.Path, read.Revision, Entries(count)));
    }

    private static List<FileHistoryEntry> Entries(int count) => [.. Enumerable.Range(0, count).Select(Entry)];

    private static FileHistoryEntry Entry(int index) =>
        new(Hash(index), "Ann", "2026-11-02", $"commit {index}", "a.txt", null, FileChangeKind.Modified);

    private static string Hash(int index) => new((char)('a' + index), 40);

    private static DiffDocument Document(int rows) => new(
        [.. Enumerable.Range(0, rows).Select(row => new DiffRow(DiffRowKind.Context, $"line {row}", row, row, 0))],
        [],
        [],
        [],
        rows);

    private static AppState Opened(params string[] names)
    {
        var state = AppState.Empty;
        foreach (var name in names)
        {
            state = AppReducer.Reduce(state, new OpenRepositoryCommand(Location(name))).State;
        }

        return state;
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-file-history-reducer-tests", name);
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
