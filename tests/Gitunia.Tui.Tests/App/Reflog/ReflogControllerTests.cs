using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Reflog;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Reflog;

public class ReflogControllerTests
{
    [Fact]
    public async Task GivenCommitsAmendResetAndCheckout_WhenOpeningTheReflog_ThenEveryActionIsListedNewestFirst()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        repository.Git("commit", "--amend", "-m", "two amended");
        repository.Git("reset", "--hard", first);
        repository.CheckoutNewBranch("topic");
        using var harness = await Harness.LoadAsync(repository);

        harness.Open();
        await harness.PumpUntilAsync(() => harness.Reflog.IsOpen);

        var state = harness.Reflog.State!;
        Assert.Equal("HEAD", state.Title);
        Assert.Equal(
            ["checkout", "reset", "commit (amend)", "commit", "commit (initial)"],
            state.Entries.Select(entry => entry.Action));
        Assert.NotNull(harness.Reflog.CurrentFrame());
    }

    [Fact]
    public async Task GivenACommitLostByResetHard_WhenResettingToItsEntry_ThenTheBranchIsBackAtTheLostCommit()
    {
        using var repository = TwoCommits();
        var lost = repository.Head;
        repository.Git("reset", "--hard", "HEAD~1");
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.MoveTo(harness.Reflog.State!.Entries.ToList().FindIndex(entry => entry.Hash == lost));

        harness.Run(ActionIds.ResetBranch);
        Assert.False(harness.Reflog.IsOpen);
        await harness.PumpUntilAsync(() => harness.Mode == KeyMode.Insert);
        harness.Reset.HandleTextKey(new KeyEvent("s"));
        await harness.PumpUntilAsync(() => harness.Refreshes > 0);

        Assert.Equal(lost, repository.Head);
    }

    [Fact]
    public async Task GivenAnEntry_WhenSelectingItsCommit_ThenTheGraphIsAskedForThatCommitAndTheListStaysOpen()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.MoveTo(harness.Reflog.State!.Entries.ToList().FindIndex(entry => entry.Hash == first));

        harness.Run(ActionIds.ReflogSelectCommit);

        Assert.Equal([first], harness.SelectedCommits);
        Assert.True(harness.Reflog.IsOpen);
    }

    [Fact]
    public async Task GivenACommitOutsideTheGraph_WhenSelectingIt_ThenTheStatusSaysSo()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRefusal = "abc1234 is not in the graph";
        await harness.OpenAsync();

        harness.Run(ActionIds.ReflogSelectCommit);

        Assert.Equal("abc1234 is not in the graph", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest.Severity);
    }

    [Fact]
    public async Task GivenAnEntry_WhenCreatingABranchThere_ThenThePromptOpensAndTheBranchStartsAtTheEntry()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.MoveTo(harness.Reflog.State!.Entries.ToList().FindIndex(entry => entry.Hash == first));

        harness.Run(ActionIds.BranchCreate);
        Assert.False(harness.Reflog.IsOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Contains(first[..7], harness.Branches.StatusLine, StringComparison.Ordinal);
        harness.Branches.HandleTextKey(new KeyEvent("r"));
        harness.Branches.HandleTextKey(new KeyEvent("x"));
        Assert.True(harness.Branches.TryExecute(ActionIds.InputAccept, null));
        await harness.PumpUntilAsync(() => harness.Refreshes > 0);

        Assert.Equal(first, repository.Git("rev-parse", "rx").Trim());
        Assert.Equal("rx", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenAnEntry_WhenCheckingItOut_ThenHeadIsDetachedAtTheEntry()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.MoveTo(harness.Reflog.State!.Entries.ToList().FindIndex(entry => entry.Hash == first));

        harness.Run(ActionIds.BranchSwitch);
        await harness.PumpUntilAsync(() => harness.Refreshes > 0);

        Assert.False(harness.Reflog.IsOpen);
        Assert.Equal(first, repository.Head);
        Assert.Equal(string.Empty, repository.Git("branch", "--show-current").Trim());
        Assert.StartsWith("HEAD is detached at", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenALocalBranchRow_WhenOpening_ThenThatBranchsReflogIsListed()
    {
        using var repository = TwoCommits();
        repository.CheckoutNewBranch("topic");
        repository.Commit("on topic", new TestFile("b.txt", "b"));
        repository.Checkout("main");
        using var harness = await Harness.LoadAsync(repository);
        harness.Selection = new BranchSelection(SidebarRowKind.LocalBranch, "topic", null);

        await harness.OpenAsync();

        Assert.Equal("topic", harness.Reflog.State!.Title);
        Assert.Equal(["commit", "branch"], harness.Reflog.State.Entries.Select(entry => entry.Action));
    }

    [Fact]
    public async Task GivenAnOpenList_WhenMovingAndClosing_ThenTheHighlightFollowsAndEveryActionIsSwallowed()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ListDown);
        Assert.Equal(1, harness.Reflog.State!.SelectedIndex);
        harness.Run(ActionIds.ListBottom);
        Assert.Equal(1, harness.Reflog.State.SelectedIndex);
        harness.Run(ActionIds.ListTop);
        Assert.Equal(0, harness.Reflog.State.SelectedIndex);
        harness.Run(ActionIds.PaneNext);
        Assert.True(harness.Reflog.IsOpen);
        harness.Run(ActionIds.NavigateBack);

        Assert.False(harness.Reflog.IsOpen);
        Assert.False(harness.Reflog.TryExecute(ActionIds.ListDown, null));
    }

    [Fact]
    public async Task GivenAnOpenList_WhenTheRepositoryMoves_ThenRefreshReadsItAgain()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        var before = harness.Reflog.State!.Entries.Count;
        repository.Commit("three", new TestFile("a.txt", "3"));

        harness.Run(ActionIds.RepositoryRefresh);
        await harness.PumpUntilAsync(() => harness.Reflog.State!.Entries.Count > before);

        Assert.Equal("three", harness.Reflog.State!.Entries[0].Message);
    }

    [Fact]
    public async Task GivenAFailingRead_WhenOpening_ThenNoListOpensAndTheErrorIsReported()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FailingReader());

        harness.Open();
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.False(harness.Reflog.IsOpen);
        var latest = harness.Statuses.Log.Latest!;
        Assert.StartsWith("Could not read the reflog of HEAD:", latest.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, latest.Severity);
    }

    [Fact]
    public async Task GivenAnEmptyReflog_WhenActingOnAnEntry_ThenNothingStartsAndTheStatusWarns()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new EmptyReader());
        await harness.OpenAsync();

        harness.Run(ActionIds.ResetBranch);

        Assert.True(harness.Reflog.IsOpen);
        Assert.Null(harness.Reflog.State!.Selected);
        Assert.Equal("No reflog entry selected", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenADetachedHead_WhenResettingToAnEntry_ThenTheRefusalIsShownAndTheListStaysOpen()
    {
        using var repository = TwoCommits();
        repository.Checkout(FirstCommit(repository));
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.ResetBranch);

        Assert.True(harness.Reflog.IsOpen);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public void GivenTheReflogSelectAction_WhenBuildingThePaletteForTheGraph_ThenItIsNotListed()
    {
        var entries = PaletteEntryBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView);

        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.ReflogSelectCommit);
        Assert.Contains(entries, entry => entry.Action == ActionIds.ReflogOpen);
    }

    [Fact]
    public async Task GivenAClickOnARow_WhenSelectingByLine_ThenThatEntryIsHighlighted()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();

        Assert.True(harness.Reflog.SelectEntryAt(2));
        Assert.Equal(1, harness.Reflog.State!.SelectedIndex);
        Assert.False(harness.Reflog.SelectEntryAt(0));
    }

    [Fact]
    public async Task GivenTypedText_WhenSearching_ThenTheHighlightFollowsAndTheKeysWork()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.SearchOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.Reflog.HandleTextKey(new KeyEvent("i")));
        Assert.True(harness.Reflog.HandleTextKey(new KeyEvent("n")));
        Assert.True(harness.Reflog.HandleTextKey(new KeyEvent("i")));
        Assert.Equal(1, harness.Reflog.State!.SelectedIndex);
        Assert.Contains("/ini_", harness.Reflog.CurrentFrame()!.Lines[^2].Text, StringComparison.Ordinal);

        harness.Run(ActionIds.InputAccept);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Reflog.State!.IsEditingSearch);
        harness.Run(ActionIds.SearchNext);
        Assert.Equal(1, harness.Reflog.State.SelectedIndex);

        harness.Run(ActionIds.Cancel);
        Assert.True(harness.Reflog.IsOpen);
        Assert.False(harness.Reflog.State!.HasSearch);
        harness.Run(ActionIds.Cancel);
        Assert.False(harness.Reflog.IsOpen);
    }

    [Fact]
    public async Task GivenNoMatch_WhenSteppingTheSearch_ThenTheStatusSaysSo()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.SearchOpen);
        harness.Reflog.HandleTextKey(new KeyEvent("z"));
        harness.Run(ActionIds.InputAccept);

        harness.Run(ActionIds.SearchNext);

        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Equal(0, harness.Reflog.State!.SelectedIndex);
    }

    [Fact]
    public async Task GivenTheSearchBox_WhenEscapeIsPressed_ThenTheSearchIsDroppedAndTheListStaysOpen()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.SearchOpen);
        harness.Reflog.HandleTextKey(new KeyEvent("c"));

        harness.Run(ActionIds.Cancel);

        Assert.True(harness.Reflog.IsOpen);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Reflog.State!.HasSearch);
    }

    [Fact]
    public async Task GivenBranches_WhenPickingARefFromTheList_ThenThatBranchReflogOpens()
    {
        using var repository = TwoCommits();
        repository.CheckoutNewBranch("topic");
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        Assert.Equal("HEAD", harness.Reflog.State!.Title);

        harness.Run(ActionIds.ReflogOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Equal("HEAD", harness.Reflog.State!.Picker!.Candidates[0]);
        Assert.Contains("topic", harness.Reflog.State.Picker.Candidates);
        harness.Reflog.HandleTextKey(new KeyEvent("t"));
        harness.Reflog.HandleTextKey(new KeyEvent("o"));
        Assert.Equal("topic", harness.Reflog.State.Picker!.Selected);
        harness.Run(ActionIds.InputAccept);
        await harness.PumpUntilAsync(() => harness.Reflog.State is { Picker: null, Title: "topic" });

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("refs/heads/topic", harness.Reflog.State!.ReferenceName);
        Assert.NotEmpty(harness.Reflog.State.Entries);
    }

    [Fact]
    public async Task GivenTheRefPicker_WhenEscapeIsPressed_ThenTheListComesBack()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ReflogOpen);

        harness.Run(ActionIds.Cancel);

        Assert.Null(harness.Reflog.State!.Picker);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.True(harness.Reflog.IsOpen);
    }

    [Fact]
    public async Task GivenTheRefPicker_WhenClickingARow_ThenThatReflogOpens()
    {
        using var repository = TwoCommits();
        repository.CheckoutNewBranch("topic");
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ReflogOpen);
        var row = harness.Reflog.State!.Picker!.Candidates.ToList().IndexOf("topic");

        Assert.True(harness.Reflog.OnLineClicked(row + 1));
        await harness.PumpUntilAsync(() => harness.Reflog.State is { Picker: null, Title: "topic" });
    }

    [Fact]
    public async Task GivenTheRefPicker_WhenClickingAnActionRowOrTypingNavigationKeys_ThenOnlyThePickerActs()
    {
        using var repository = TwoCommits();
        repository.CheckoutNewBranch("topic");
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ReflogOpen);

        Assert.False(harness.Reflog.OnLineClicked(harness.ActionLine(0) + 5));
        harness.Reflog.HandleTextKey(new KeyEvent("Down"));
        var moved = harness.Reflog.State!.Picker!.Selected;
        harness.Reflog.HandleTextKey(new KeyEvent("Up"));

        Assert.NotEqual(moved, harness.Reflog.State.Picker!.Selected);
        Assert.Equal(0, harness.Reflog.State.SelectedIndex);
        harness.Run(ActionIds.AppQuit);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenAReadInFlight_WhenAcceptingTheSearchBoxClick_ThenNothingRuns()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.SearchOpen);

        Assert.False(harness.Reflog.OnLineClicked(harness.ActionLine(1)));
        Assert.True(harness.Reflog.State!.IsEditingSearch);
    }

    [Fact]
    public async Task GivenAnEntry_WhenClickingTheSelectRow_ThenTheGraphIsAskedForItsCommit()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        Assert.True(harness.Reflog.SelectEntryAt(2));

        Assert.True(harness.Reflog.ActivateLine(harness.ActionLine(0)));

        Assert.Equal([first], harness.SelectedCommits);
        Assert.True(harness.Reflog.IsOpen);
    }

    [Fact]
    public async Task GivenAnEntry_WhenClickingTheResetRow_ThenTheResetPromptOpensAtIt()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        Assert.True(harness.Reflog.SelectEntryAt(2));

        Assert.True(harness.Reflog.ActivateLine(harness.ActionLine(1)));

        Assert.False(harness.Reflog.IsOpen);
        await harness.PumpUntilAsync(() => harness.Mode == KeyMode.Insert);
    }

    [Fact]
    public async Task GivenAnEntry_WhenClickingTheBranchRow_ThenTheBranchPromptOpens()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();

        Assert.True(harness.Reflog.ActivateLine(harness.ActionLine(2)));

        Assert.False(harness.Reflog.IsOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
    }

    [Fact]
    public async Task GivenAnEntry_WhenClickingTheCheckoutRow_ThenHeadIsDetachedAtIt()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        Assert.True(harness.Reflog.SelectEntryAt(2));

        Assert.True(harness.Reflog.ActivateLine(harness.ActionLine(3)));
        await harness.PumpUntilAsync(() => harness.Refreshes > 0);

        Assert.False(harness.Reflog.IsOpen);
        Assert.Equal(first, repository.Head);
        Assert.False(harness.Reflog.ActivateLine(0));
    }

    [Fact]
    public async Task GivenAnotherAction_WhenTheListIsClosed_ThenItIsNotConsumed()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);

        Assert.False(harness.Reflog.TryExecute(ActionIds.ListDown, null));
    }

    private static TestRepository TwoCommits() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));

    private static string FirstCommit(TestRepository repository) =>
        repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

    private sealed class FailingReader : IReflogReader
    {
        public Task<ParseResult<ReflogEntry>> ReadAsync(
            RepositoryLocation location,
            string reference,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromException<ParseResult<ReflogEntry>>(new InvalidOperationException("boom"));
    }

    private sealed class EmptyReader : IReflogReader
    {
        public Task<ParseResult<ReflogEntry>> ReadAsync(
            RepositoryLocation location,
            string reference,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ParseResult<ReflogEntry>([], []));
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
        private TabSession? _session;

        private Harness(IReflogReader? reader)
        {
            Reset = new ResetActions(
                _host,
                TestGit.Services.Reset,
                TestGit.Services.Status,
                new RepositoryOperationDetector(),
                Statuses,
                _pump.Post,
                _ => TestContext.Current.CancellationToken,
                (_, _) => true,
                CountRefresh,
                mode => Mode = mode,
                () => { },
                () => Selection,
                new TabOperationGate());
            Branches = new BranchActions(
                _host,
                TestGit.Services.Branches,
                Statuses,
                _pump.Post,
                _ => TestContext.Current.CancellationToken,
                (_, _) => true,
                CountRefresh,
                mode => Mode = mode,
                () => { },
                () => Selection);
            Reflog = new ReflogController(
                _host,
                reader ?? TestGit.Services.Reflog,
                Reset,
                Branches,
                Statuses,
                _pump.Post,
                _ => TestContext.Current.CancellationToken,
                SelectCommit,
                () => Selection,
                () => new ScreenSize(100, 30),
                GraphGlyphSet.Unicode,
                mode => Mode = mode,
                () => { });
        }

        public ReflogController Reflog { get; }

        public ResetActions Reset { get; }

        public BranchActions Branches { get; }

        public StatusChannel Statuses { get; } = new();

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public int Refreshes { get; private set; }

        public BranchSelection Selection { get; set; } = new(null, null, null);

        public List<string> SelectedCommits { get; } = [];

        public string SelectRefusal { get; set; } = string.Empty;

        public static async Task<Harness> LoadAsync(TestRepository repository, IReflogReader? reader = null)
        {
            var harness = new Harness(reader);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            harness._host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = harness._host.Workspace.ActiveTabId!.Value;
            harness._session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                harness._session,
                harness._host,
                new StatusChannel(),
                InlineUi.Post,
                harness._session.Cancellation.Token);
            return harness;
        }

        public void Open() => Assert.True(Reflog.TryExecute(ActionIds.ReflogOpen, null));

        public async Task OpenAsync()
        {
            Open();
            await PumpUntilAsync(() => Reflog.IsOpen);
        }

        public void MoveTo(int index)
        {
            Assert.True(index >= 0);
            Reflog.TryExecute(ActionIds.ListTop, index + 1);
        }

        // The frame line of the nth action row under the entries.
        public int ActionLine(int index) => 1 + Reflog.State!.Entries.Count + index;

        public void Run(ActionId action) => Assert.True(Reflog.TryExecute(action, null), action.Value);

        public Task PumpUntilAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose() => _session?.Dispose();

        private string SelectCommit(TabId tabId, string hash)
        {
            SelectedCommits.Add(hash);
            return SelectRefusal;
        }

        private bool CountRefresh(TabId tabId)
        {
            Refreshes++;
            return true;
        }
    }
}
