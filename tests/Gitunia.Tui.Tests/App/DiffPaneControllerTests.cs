using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class DiffPaneControllerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFileRow_WhenOpeningTheDiff_ThenTheDiffOpensFocusedAtThatFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.SelectFile("b.txt");

        harness.Execute(ActionIds.DetailOpenDiff);
        await harness.WaitForDiffAsync();

        Assert.True(harness.Session.Diff!.IsOpen);
        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAnInfoRow_WhenOpeningTheDiff_ThenNothingOpensAndANoticeSaysWhy()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.DetailList.SelectedItem = 0;

        harness.Execute(ActionIds.DetailOpenDiff);

        Assert.False(harness.Session.Diff!.IsOpen);
        Assert.Equal("Select a file to see its diff", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenJumpingBetweenHunksAndFiles_ThenTheCursorLandsOnTheirHeaders()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;
        Assert.True(document.HunkRows.Count >= 2);
        Assert.True(document.FileRows.Count >= 2);

        harness.Execute(ActionIds.DiffNextHunk);
        var firstAfterStart = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffNextHunk);
        harness.Execute(ActionIds.DiffPreviousHunk);
        var back = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffNextFile);

        Assert.Contains(firstAfterStart!.Value, document.HunkRows);
        Assert.Equal(firstAfterStart, back);
        Assert.Equal(document.FileRows[1], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenTheLastHunk_WhenJumpingForward_ThenTheCursorStaysAndANoticeSaysSo()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.ListBottom);

        harness.Execute(ActionIds.DiffNextHunk);

        Assert.Equal(harness.Session.Diff!.Source.Count - 1, harness.DiffList.SelectedItem);
        Assert.Equal("No next hunk", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenSearching_ThenMatchesAreHighlightedAndNStepsThroughThem()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffSearchOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("line");
        Assert.StartsWith("/line_", harness.Controller.StatusLine, StringComparison.Ordinal);
        harness.Execute(ActionIds.InputAccept);
        var first = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffSearchNext);
        var second = harness.DiffList.SelectedItem;

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("line", harness.Session.Diff!.Source.Highlight);
        Assert.True(second > first);
    }

    [Fact]
    public async Task GivenAnActiveSearch_WhenGoingBack_ThenTheSearchClearsBeforeThePaneCloses()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("line");
        harness.Execute(ActionIds.InputAccept);

        harness.Execute(ActionIds.NavigateBack);

        Assert.True(harness.Session.Diff!.IsOpen);
        Assert.Null(harness.Session.Diff.Source.Highlight);
        harness.Execute(ActionIds.NavigateBack);
        Assert.False(harness.Session.Diff.IsOpen);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
        Assert.Empty(harness.Session.Diff.Source.Document.Rows);
    }

    [Fact]
    public async Task GivenAnOpenSearchBox_WhenCancelling_ThenTheModeIsNormalAgain()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("x");

        harness.Execute(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
        Assert.True(harness.Session.Diff!.IsOpen);
    }

    [Fact]
    public async Task GivenAFocusedPane_WhenZooming_ThenItFillsTheScreenUntilZoomedAgainOrFocusLeaves()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.PaneZoom);
        Assert.Equal(PaneKind.Diff, harness.Controller.Arrangement().Zoomed);
        harness.Execute(ActionIds.PaneZoom);
        Assert.Null(harness.Controller.Arrangement().Zoomed);
        harness.Execute(ActionIds.PaneZoom);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.Null(harness.Controller.Arrangement().Zoomed);
        Assert.True(harness.Controller.Arrangement().IsDiffOpen);
    }

    [Fact]
    public async Task GivenTheSidebarIsFocused_WhenZooming_ThenANoticeSaysItCannot()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.Send(new FocusPaneCommand(PaneKind.Sidebar));

        harness.Execute(ActionIds.PaneZoom);

        Assert.Equal("The sidebar cannot be zoomed", harness.Controller.Notice);
        Assert.Null(harness.Controller.Arrangement().Zoomed);
    }

    [Fact]
    public async Task GivenAMergeCommitSelected_WhenOpeningAFile_ThenTheFirstParentDiffShows()
    {
        using var repository = TestRepository.Create()
            .Commit("root", ("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", ("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", ("main.txt", "main\n"))
            .Merge("side");
        using var harness = await DiffHarness.LoadAsync(repository);
        harness.SelectFile("side.txt");

        harness.Execute(ActionIds.DetailOpenDiff);
        await harness.WaitForDiffAsync();

        Assert.Equal(["side.txt"], harness.Session.Diff!.Source.Document.FilePaths.Select(names => names[0]));
    }

    [Fact]
    public async Task GivenTwoTabs_WhenSwitchingAway_ThenTheDiffCursorIsRestored()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffNextHunk);
        var row = harness.DiffList.SelectedItem;

        harness.Controller.OnTabSwitched(harness.Session, null);
        Assert.Null(harness.DiffList.Source);
        harness.Controller.OnTabSwitched(null, harness.Session);

        Assert.Equal(row, harness.DiffList.SelectedItem);
        Assert.Same(harness.Session.Diff!.Source, harness.DiffList.Source);
    }

    private sealed class DiffHarness : IDisposable
    {
        private readonly TestRepository? ownedRepository;
        private readonly KeyHolder keys = new();

        private DiffHarness(WorkspaceHost host, TabSession session, TestRepository? ownedRepository)
        {
            Host = host;
            Session = session;
            this.ownedRepository = ownedRepository;
            DetailList = new ListView { Source = session.DetailSource };
            DiffList = new ListView();
            Controller = new DiffPaneController(host, () => Session, DetailList, DiffList, mode => keys.Mode = mode, () => 40, () => { }, () => { });
            Controller.OnTabSwitched(null, session);
        }

        public WorkspaceHost Host { get; }

        public TabSession Session { get; }

        public ListView DetailList { get; }

        public ListView DiffList { get; }

        public DiffPaneController Controller { get; }

        public KeyMode Mode => keys.Mode;

        public RepoState Tab => Host.Workspace.ActiveTab!;

        public static async Task<DiffHarness> LoadAsync(TestRepository? repository = null)
        {
            var owned = repository is null ? CreateDefaultRepository() : null;
            var path = (repository ?? owned)!.Path;
            var runner = new GitRunner();
            var location = await RepositoryDiscovery.DiscoverAsync(runner, path, Token);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var repo = new Repo(runner, location);
            var session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(repo, action => action(), TimeSpan.Zero),
                new DiffStore(repo, action => action()));
            await RepoLoader.RunAsync(runner, location, session, host, InlineUi.Post, session.Cancellation.Token);
            var harness = new DiffHarness(host, session, owned);
            await harness.ShowDetailsOfAsync(session.Graph[0].Id);
            return harness;
        }

        public void Send(RepoCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

        public void Execute(ActionId action, int? count = null) => Controller.TryExecute(action, count);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString(), KeyModifiers.None));
            }
        }

        public void SelectFile(string path)
        {
            Send(new FocusPaneCommand(PaneKind.Detail));
            var content = Session.DetailSource.Content;
            var index = content.Files.ToList().FindIndex(file => file.Path == path);
            Assert.True(index >= 0, $"{path} is not in the file list");
            DetailList.SelectedItem = content.FirstFileRow + index;
        }

        public async Task OpenAsync(string path)
        {
            SelectFile(path);
            Execute(ActionIds.DetailOpenDiff);
            await WaitForDiffAsync();
        }

        public async Task WaitForDiffAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (Session.Diff!.Store.Status != DiffStatus.Loaded)
            {
                Assert.True(DateTime.UtcNow < deadline, "the diff did not load");
                await Task.Delay(10, Token);
            }
        }

        public void Dispose()
        {
            Session.Dispose();
            ownedRepository?.Dispose();
        }

        private static TestRepository CreateDefaultRepository() => TestRepository.Create()
            .Commit("root", ("a.txt", string.Concat(Enumerable.Range(1, 40).Select(index => $"line {index}\n"))), ("b.txt", "b\n"))
            .Commit(
                "edit",
                ("a.txt", string.Concat(Enumerable.Range(1, 40).Select(index => index is 3 or 38 ? $"line {index} changed\n" : $"line {index}\n"))),
                ("b.txt", "b changed\n"));

        private async Task ShowDetailsOfAsync(ObjectId commit)
        {
            Session.Details.Select(commit);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (Session.Details.Current.Status != CommitDetailsStatus.Loaded)
            {
                Assert.True(DateTime.UtcNow < deadline, "the details did not load");
                await Task.Delay(10, Token);
            }

            var entry = Session.Graph[0];
            Session.DetailSource.Update(DetailContentBuilder.Build(entry, Session.Details.Current));
            Send(new FocusPaneCommand(PaneKind.Detail));
        }
    }

    private sealed class KeyHolder
    {
        public KeyMode Mode { get; set; } = KeyMode.Normal;
    }
}
