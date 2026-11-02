using Gitunia.Tui.Views;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Input;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Tests.Rendering.Graph;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class NavigationActionsTests
{
    private const int CommitCount = 5;

    [Fact]
    public void GivenTheLogIsStillLoading_WhenPressingG_ThenTheSelectionIsTheLastLoadedCommitAndTheHintShows()
    {
        using var harness = new NavigationHarness(isLoading: true);

        harness.Actions.TryExecute(ActionIds.ListBottom, null);

        Assert.Equal(NavigationActions.StillLoadingNotice, harness.Actions.Notice);
        Assert.Equal(CommitCount - 1 + ScreenLayout.ReservedLeadingRows, harness.GraphList.SelectedItem);
    }

    [Fact]
    public void GivenTheLogIsCutAtTheSoftLimit_WhenPressingG_ThenTheRestIsRequestedAndTheHintShows()
    {
        using var harness = new NavigationHarness(isLoading: false);
        harness.Session.Truncate(500);
        var requests = 0;
        harness.Session.ExpandRequested += () => requests++;

        harness.Actions.TryExecute(ActionIds.ListBottom, null);

        Assert.Equal(1, requests);
        Assert.True(harness.Session.IsLoading);
        Assert.Equal(NavigationActions.StillLoadingNotice, harness.Actions.Notice);
        Assert.Equal(CommitCount - 1 + ScreenLayout.ReservedLeadingRows, harness.GraphList.SelectedItem);
    }

    [Fact]
    public void GivenTheLogIsCutAtTheSoftLimit_WhenPressingGWithACount_ThenTheRestIsNotRequested()
    {
        using var harness = new NavigationHarness(isLoading: false);
        harness.Session.Truncate(500);
        var requests = 0;
        harness.Session.ExpandRequested += () => requests++;

        harness.Actions.TryExecute(ActionIds.ListBottom, 3);

        Assert.Equal(0, requests);
    }

    [Fact]
    public void GivenTheLogIsLoaded_WhenPressingG_ThenThereIsNoHint()
    {
        using var harness = new NavigationHarness(isLoading: false);

        harness.Actions.TryExecute(ActionIds.ListBottom, null);

        Assert.Equal(string.Empty, harness.Actions.Notice);
    }

    [Fact]
    public void GivenAScreenUnder14Rows_WhenOpeningDetails_ThenTheNoticeSaysTheScreenIsTooShort()
    {
        using var harness = new NavigationHarness(isLoading: false, screenHeight: 13);

        harness.Actions.TryExecute(ActionIds.GraphOpenDetails, null);

        Assert.Equal("The details pane needs a taller screen", harness.Actions.Notice);
    }

    [Fact]
    public void GivenAScreenOf14Rows_WhenOpeningDetails_ThenThereIsNoNotice()
    {
        using var harness = new NavigationHarness(isLoading: false, screenHeight: 14);

        harness.Actions.TryExecute(ActionIds.GraphOpenDetails, null);

        Assert.Equal(string.Empty, harness.Actions.Notice);
    }

    [Fact]
    public void GivenTheLogIsStillLoading_WhenMovingDown_ThenThereIsNoHint()
    {
        using var harness = new NavigationHarness(isLoading: true);

        harness.Actions.TryExecute(ActionIds.ListDown, null);

        Assert.Equal(string.Empty, harness.Actions.Notice);
    }

    [Fact]
    public void GivenTheHintShows_WhenTheLoadFinishes_ThenTheHintIsGone()
    {
        using var harness = new NavigationHarness(isLoading: true);
        var session = harness.Session;
        var handlers = new Dictionary<InputHandler, IActionHandler>();
        var textKeys = new Dictionary<InputHandler, ITextKeyHandler>();
        foreach (var handler in Enum.GetValues<InputHandler>())
        {
            handlers[handler] = harness.Actions;
            textKeys[handler] = new NoTextKeys();
        }

        var executor = new ActionExecutor(new InputHandlers(handlers, textKeys), new NoRepeat(), new FakeActiveSessions(() => session));
        executor.RouteAction(new RouteActionEffect(ActionIds.ListBottom, null, [InputHandler.Navigation]));
        Assert.Equal(NavigationActions.StillLoadingNotice, executor.Message);

        harness.Session.Complete();

        Assert.Equal(string.Empty, executor.Message);
    }

    [Fact]
    public void GivenTheGraphPane_WhenAppQuitRuns_ThenTheApplicationQuits()
    {
        using var harness = new NavigationHarness(isLoading: false);

        harness.Actions.TryExecute(ActionIds.AppQuit, null);

        Assert.Equal(1, harness.QuitCount);
    }

    [Fact]
    public void GivenTheGraphPane_WhenNavigateBackRuns_ThenTheApplicationDoesNotQuit()
    {
        using var harness = new NavigationHarness(isLoading: false);

        harness.Actions.TryExecute(ActionIds.NavigateBack, null);

        Assert.Equal(0, harness.QuitCount);
    }

    private sealed class NoTextKeys : ITextKeyHandler
    {
        public bool HandleTextKey(KeyEvent key) => false;
    }

    private sealed class NoRepeat : IRepeatStarter
    {
        public void AfterPaneAction(ActionId action)
        {
        }
    }

    private sealed class NavigationHarness : IDisposable
    {
        private readonly TerminalTabSession _session;

        private readonly CountingQuit _quit = new();

        public NavigationHarness(bool isLoading, int screenHeight = 40)
        {
            var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
            _session = new TerminalTabSession(
                new TabSessionParts(
                    new TabId(1),
                    new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)),
                GraphGlyphSet.Unicode);
            var commits = Enumerable.Range(1, CommitCount).Select(number => CommitFactory.Commit(number, $"commit {number}", number == CommitCount ? [] : [number + 1])).ToArray();
            var graph = CommitFactory.Graph(commits);
            _session.Append(Enumerable.Range(0, graph.Count).Select(index => graph[index]).ToArray());
            if (!isLoading)
            {
                _session.Complete();
            }

            GraphList = new ListView { Source = _session.Source, Width = 40, Height = 10 };
            var detailList = new ListView { Source = _session.DetailSource };
            var statuses = new StatusReporter(new Dispatcher(AppState.Empty));
            var host = new Dispatcher(AppState.Empty);
            var copier = new ClipboardCopier(new ClipboardWriter(new StringWriter(), new Osc52Options(Osc52Mode.Direct, 100_000)), statuses);
            var sessions = new FakeActiveSessions(() => _session);
            Actions = new NavigationActions(
                host,
                new FakeGraphDetailLists(GraphList, detailList),
                sessions,
                new NavigationServices(
                    _quit,
                    new FakePaneChrome(new ScreenSize(120, screenHeight)),
                    new StatusActionHandler(statuses, sessions),
                    copier));
        }

        public int QuitCount => _quit.Requests;

        public TerminalTabSession Session => _session;

        public ListView GraphList { get; }

        public NavigationActions Actions { get; }

        public void Dispose() => _session.Dispose();
    }
}
