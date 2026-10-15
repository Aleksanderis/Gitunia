using System.Drawing;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Graph;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class WorkingDiffControllerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAModifiedFileUnderTheCursor_WhenOpeningTheView_ThenItsWorkingTreeDiffLoads()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        Assert.Equal("M a.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Removed, Text: "two" });
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "TWO" });
        Assert.Contains("unstaged", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheCursorMovesDownTheList_WhenItReachesAnUntrackedFile_ThenTheFileShowsAsAdditions()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        await harness.ExecuteAsync(ActionIds.ListDown);

        Assert.Equal("A new.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "brand new" });
    }

    [Fact]
    public async Task GivenTheStagedList_WhenItTakesFocus_ThenTheIndexDiffOfItsFileShows()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        await harness.ExecuteAsync(ActionIds.PaneNext);

        Assert.Equal("M b.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "staged" });
        Assert.Contains("staged", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAListFocused_WhenGoingIntoTheDiffAndBack_ThenTheViewStaysAndTheLeaveKeysStepOutOneLevelAtATime()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());

        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);

        await harness.ExecuteAsync(ActionIds.NavigateBack);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);

        await harness.ExecuteAsync(ActionIds.NavigateBack);
        Assert.Equal(TabViewMode.History, harness.Tab.View.Mode);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenMovingDown_ThenTheDiffCursorMovesAndTheListSelectionStays()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.ListDown, 2);

        Assert.Equal(2, harness.DiffList.SelectedItem);
        Assert.Equal("a.txt", harness.Tab.WorkingTree!.Selection!.Path);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenJumpingBetweenHunks_ThenTheCursorLandsOnTheHunkHeaders()
    {
        var original = string.Concat(Enumerable.Range(1, 40).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", new TestFile("long.txt", original));
        repository.WriteFile("long.txt", original.Replace("line 2\n", "two\n").Replace("line 38\n", "thirty-eight\n"));
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffNextHunk);
        await harness.ExecuteAsync(ActionIds.DiffNextHunk);

        var document = harness.Document();
        Assert.Equal(2, document.HunkRows.Count);
        Assert.Equal(document.HunkRows[1], harness.DiffList.SelectedItem);
        await harness.ExecuteAsync(ActionIds.DiffPreviousHunk);
        Assert.Equal(document.HunkRows[0], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAShownFileEditedAgainWithTheSameStatus_WhenTheStatusIsRead_ThenTheDiffReloadsAndKeepsTheCursor()
    {
        using var repository = DefaultRepository();
        using var harness = await Harness.OpenAsync(repository);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        await harness.ExecuteAsync(ActionIds.ListDown, 3);
        var cursor = harness.DiffList.SelectedItem;
        repository.WriteFile("a.txt", "one\nTWO\nthree\nfour\n");

        await harness.RefreshStatusAsync();

        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "four" });
        Assert.Equal(cursor, harness.DiffList.SelectedItem);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAStatusThatChanged_WhenItIsLoaded_ThenTheDiffOfTheFileUnderTheCursorLoadsAgain()
    {
        using var repository = DefaultRepository();
        using var harness = await Harness.OpenAsync(repository);
        repository.Git("add", "a.txt");

        await harness.RefreshStatusAsync();

        Assert.DoesNotContain(harness.Tab.WorkingTree!.Unstaged, entry => entry.Path == "a.txt");
        Assert.Equal("A new.txt", harness.Rows()[harness.Document().FileRows[0]]);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenSearching_ThenTheCursorLandsOnTheMatchAndEscapeEndsTheSearch()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffSearchOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("TWO");
        harness.Controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("TWO", harness.Document().Rows[harness.CursorRow()].Text, ignoreCase: true);

        await harness.ExecuteAsync(ActionIds.NavigateBack);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
        Assert.Empty(harness.Controller.StatusLine);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenChangingOptions_ThenTheFileLoadsAgainWithThem()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffToggleWholeFile);

        Assert.True(harness.Session.WorkingDiff!.Store.Options.WholeFile);
        Assert.Equal(DiffStatus.Loaded, harness.Session.WorkingDiff.Store.Status);
        Assert.Contains("[file]", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenTogglingWrapAndSideBySide_ThenTheSourceFollows()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);

        await harness.ExecuteAsync(ActionIds.DiffToggleWrap);
        await harness.ExecuteAsync(ActionIds.DiffToggleSideBySide);

        Assert.True(harness.Session.WorkingDiff!.Source.IsWrapped);
        Assert.True(harness.Session.WorkingDiff.Source.IsSideBySide);
        Assert.Contains("[wrap]", harness.Controller.TitleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDiffFocused_WhenCopyingTheLine_ThenTheClipboardGetsItWithItsPrefix()
    {
        using var harness = await Harness.OpenAsync(DefaultRepository());
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        harness.DiffList.SelectedItem = harness.Document().Rows.ToList().FindIndex(row => row.Kind == DiffRowKind.Added);

        await harness.ExecuteAsync(ActionIds.DiffCopyLine);

        Assert.Contains("diff line", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnEmptyWorkingTree_WhenOpeningTheView_ThenTheDiffBoxSaysNoFileIsSelected()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        using var harness = await Harness.OpenAsync(repository);

        Assert.Equal("No file selected", Assert.Single(harness.Document().Rows).Text);
        await harness.ExecuteAsync(ActionIds.WorkingTreeOpenDiff);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenADeletedFile_WhenSelected_ThenItsLinesShowAsRemoved()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("gone.txt", "x\ny\n"));
        repository.Delete("gone.txt");
        using var harness = await Harness.OpenAsync(repository);

        Assert.Equal("D gone.txt", harness.Rows()[harness.Document().FileRows[0]]);
        Assert.Equal(["x", "y"], harness.Document().Rows.Where(row => row.Kind == DiffRowKind.Removed).Select(row => row.Text));
    }

    [Fact]
    public async Task GivenAStagedRename_WhenSelected_ThenTheDiffNamesBothPaths()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("old.txt", "a\nb\nc\nd\ne\nf\ng\nh\n"));
        repository.Git("mv", "old.txt", "new.txt");
        using var harness = await Harness.OpenAsync(repository);

        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
        Assert.Equal("R100 old.txt -> new.txt", harness.Rows()[harness.Document().FileRows[0]]);
    }

    [Fact]
    public async Task GivenAnUntrackedBinaryFile_WhenSelected_ThenTheDiffSaysThatItIsBinary()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "logo.bin"), [0, 1, 2, 3, 0, 255]);
        using var harness = await Harness.OpenAsync(repository);

        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Note, Text: "Binary file, content not shown" });
    }

    [Fact]
    public async Task GivenAConflictedFile_WhenSelected_ThenTheCombinedDiffShowsTheConflictMarkers()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\nc\n"));
        repository.CheckoutNewBranch("topic").Commit("topic", new TestFile("f.txt", "a\ntheirs\nc\n"));
        repository.Checkout("main").Commit("main", new TestFile("f.txt", "a\nours\nc\n"));
        repository.MergeExpectingConflict("topic");
        using var harness = await Harness.OpenAsync(repository);

        Assert.StartsWith("@@@", harness.Document().Rows.First(row => row.Kind == DiffRowKind.HunkHeader).Text, StringComparison.Ordinal);
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "<<<<<<< HEAD" });
        Assert.Contains(harness.Document().Rows, row => row is { Kind: DiffRowKind.Added, Text: "theirs" });
    }

    [Fact]
    public async Task GivenAFileWithAGlobInItsName_WhenSelected_ThenOnlyThatFilesDiffShows()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a*.txt", "1\n"), new TestFile("ab.txt", "1\n"));
        repository.WriteFile("a*.txt", "star\n").WriteFile("ab.txt", "b\n");
        using var harness = await Harness.OpenAsync(repository);

        Assert.Single(harness.Document().FileRows);
        Assert.Equal("M a*.txt", harness.Rows()[harness.Document().FileRows[0]]);
    }

    [Fact]
    public async Task GivenAPathThatIsNotValidUtf8_WhenSelected_ThenTheDiffBoxExplainsItCannotBeShown()
    {
        using var repository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        using var harness = await Harness.OpenAsync(repository);
        var entry = new StatusEntry(
            StatusEntryKind.Untracked,
            $"bad{(char)0xDCE9}.txt",
            null,
            FileChangeKind.Unmodified,
            FileChangeKind.Unmodified,
            null,
            ConflictKind.None,
            null);

        harness.Session.WorkingDiff!.Store.Show(new WorkingDiffRequest(WorkingDiffSide.Unstaged, entry), 99);
        await harness.WaitAsync(() => harness.Session.WorkingDiff.Store.Status == DiffStatus.Failed);

        Assert.Contains("not valid UTF-8", harness.Document().Rows[0].Text, StringComparison.Ordinal);
    }

    private static TestRepository DefaultRepository()
    {
        var repository = TestRepository.Create().Commit(
            "base",
            new TestFile("a.txt", "one\ntwo\nthree\n"),
            new TestFile("b.txt", "b\n"));
        repository.WriteFile("a.txt", "one\nTWO\nthree\n");
        repository.WriteFile("new.txt", "brand new\n");
        repository.WriteFile("b.txt", "b\nstaged\n");
        repository.Git("add", "b.txt");
        return repository;
    }

    private sealed class Harness : IDisposable
    {
        private readonly UiPump _pump;
        private readonly TestRepository _repository;
        private KeyMode _mode = KeyMode.Normal;

        private Harness(TestRepository repository, WorkspaceHost host, TabSession session, UiPump pump)
        {
            _repository = repository;
            _pump = pump;
            Host = host;
            Session = session;
            Statuses = new StatusChannel();
            DiffList = new ListView { X = 0, Y = 0, Width = 60, Height = 20 };
            DiffList.SetRelativeLayout(new Size(80, 24));
            var copier = new ClipboardCopier(new ClipboardWriter(new StringWriter(), new Osc52Options(Osc52Mode.Direct, 100_000)), Statuses);
            Controller = new WorkingDiffController(host, () => Session, DiffList, mode => _mode = mode, () => { }, copier);
            Controller.OnTabSwitched(null, session);
            Handler = new WorkingTreeActionHandler(host, () => 10, copier) { Diff = Controller };
        }

        public WorkspaceHost Host { get; }

        public TabSession Session { get; }

        public StatusChannel Statuses { get; }

        public ListView DiffList { get; }

        public WorkingDiffController Controller { get; }

        public WorkingTreeActionHandler Handler { get; }

        public KeyMode Mode => _mode;

        public RepositoryState Tab => Host.Workspace.ActiveTab!;

        public static async Task<Harness> OpenAsync(TestRepository repository)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pump = new UiPump(Token);
            var session = new TabSession(
                host.Workspace.ActiveTabId!.Value,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, pump.Post, TimeSpan.Zero),
                workingDiffStore: new WorkingDiffStore(TestGit.Services.Diff, location, pump.Post));
            var harness = new Harness(repository, host, session, pump);
            await harness.LoadStatusAsync();
            harness.Handler.TryExecute(ActionIds.WorkingTreeOpen, null);
            await harness.WaitForLoadedAsync();
            return harness;
        }

        public DiffDocument Document() => Session.WorkingDiff!.Store.Document;

        public IReadOnlyList<string> Rows() => [.. Document().Rows.Select(row => row.Text)];

        public int CursorRow() => Session.WorkingDiff!.Source.RowOf(DiffList.SelectedItem ?? 0);

        public async Task ExecuteAsync(ActionId action, int? count = null)
        {
            Handler.TryExecute(action, count);
            await WaitForLoadedAsync();
        }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString()));
            }
        }

        public async Task RefreshStatusAsync()
        {
            var snapshot = await ReadStatusAsync();
            Host.Dispatch(new TabCommand(Tab.Id, new WorkingTreeLoadedCommand(snapshot)));
            if (Session.WorkingDiff!.Store.Status != DiffStatus.Loading)
            {
                // The letters did not change: the refresher tells the session so the shown file is read again.
                Session.NotifyStatusRead();
            }

            await WaitForLoadedAsync();
        }

        public Task WaitAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose()
        {
            Session.Dispose();
            _repository.Dispose();
        }

        private async Task LoadStatusAsync()
        {
            var snapshot = await ReadStatusAsync();
            Host.Dispatch(new TabCommand(Tab.Id, new WorkingTreeLoadedCommand(snapshot)));
        }

        private async Task<StatusSnapshot> ReadStatusAsync() =>
            await TestGit.Services.Status.ReadStatusAsync(Tab.Location, false, UntrackedFilesMode.All, Token);

        private async Task WaitForLoadedAsync()
        {
            var store = Session.WorkingDiff!.Store;
            await _pump.RunUntilAsync(() => store.Status != DiffStatus.Loading);
        }
    }
}
