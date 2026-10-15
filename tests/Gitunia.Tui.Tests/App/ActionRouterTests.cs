using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class ActionRouterTests
{
    private const int CommitCount = 5;

    [Fact]
    public void GivenTheLogIsStillLoading_WhenPressingG_ThenTheSelectionIsTheLastLoadedCommitAndTheHintShows()
    {
        using var harness = new RouterHarness(isLoading: true);

        harness.Router.Execute(ActionIds.ListBottom, null);

        Assert.Equal(ActionRouter.StillLoadingNotice, harness.Router.Message);
        Assert.Equal(CommitCount - 1 + ScreenLayout.ReservedLeadingRows, harness.GraphList.SelectedItem);
    }

    [Fact]
    public void GivenTheLogIsLoaded_WhenPressingG_ThenThereIsNoHint()
    {
        using var harness = new RouterHarness(isLoading: false);

        harness.Router.Execute(ActionIds.ListBottom, null);

        Assert.Equal(string.Empty, harness.Router.Message);
    }

    [Fact]
    public void GivenTheLogIsStillLoading_WhenMovingDown_ThenThereIsNoHint()
    {
        using var harness = new RouterHarness(isLoading: true);

        harness.Router.Execute(ActionIds.ListDown, null);

        Assert.Equal(string.Empty, harness.Router.Message);
    }

    [Fact]
    public void GivenTheHintShows_WhenTheLoadFinishes_ThenTheHintIsGone()
    {
        using var harness = new RouterHarness(isLoading: true);
        harness.Router.Execute(ActionIds.ListBottom, null);

        harness.Session.Complete();

        Assert.Equal(string.Empty, harness.Router.Message);
    }

    [Fact]
    public void GivenTheGraphPane_WhenAppQuitRuns_ThenTheApplicationQuits()
    {
        using var harness = new RouterHarness(isLoading: false);

        harness.Router.Execute(ActionIds.AppQuit, null);

        Assert.Equal(1, harness.QuitCount);
    }

    [Fact]
    public void GivenTheGraphPane_WhenNavigateBackRuns_ThenTheApplicationDoesNotQuit()
    {
        using var harness = new RouterHarness(isLoading: false);

        harness.Router.Execute(ActionIds.NavigateBack, null);

        Assert.Equal(0, harness.QuitCount);
    }

    private sealed class RouterHarness : IDisposable
    {
        private readonly TabSession _session;

        public RouterHarness(bool isLoading)
        {
            var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
            _session = new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
            var commits = Enumerable.Range(1, CommitCount).Select(number => CommitFactory.Commit(number, $"commit {number}", number == CommitCount ? [] : [number + 1])).ToArray();
            var graph = CommitFactory.Graph(commits);
            _session.Append(Enumerable.Range(0, graph.Count).Select(index => graph[index]).ToArray());
            if (!isLoading)
            {
                _session.Complete();
            }

            GraphList = new ListView { Source = _session.Source, Width = 40, Height = 10 };
            var detailList = new ListView { Source = _session.DetailSource };
            var statuses = new StatusChannel();
            var host = new WorkspaceHost(Workspace.Empty);
            var copier = new ClipboardCopier(new ClipboardWriter(new StringWriter(), new Osc52Options(Osc52Mode.Direct, 100_000)), statuses);
            var sidebar = new SidebarActionHandler(host, new SidebarListSource(GraphGlyphSet.Unicode), () => _session, () => 10, () => false, () => false, _ => { }, _ => { }, copier);
            Router = new ActionRouter(host, GraphList, detailList, () => _session, () => QuitCount++, sidebar, statuses, copier);
        }

        public int QuitCount { get; private set; }

        public TabSession Session => _session;

        public ListView GraphList { get; }

        public ActionRouter Router { get; }

        public void Dispose() => _session.Dispose();
    }
}
