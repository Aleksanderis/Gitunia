using Gitunia.App;
using Gitunia.App.Reflog;
using Gitunia.App.Status;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core;
using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Reflog;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Reflog;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.App.Reflog;

public class ReflogInputTests
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
        harness.Host.Dispatch(new PromptKeyPressedCommand(new KeyEvent("s")));

        var prepare = Assert.Single(harness.Effects.OfType<PrepareResetEffect>());
        Assert.Equal(new ResetTarget(lost, "main", ResetMode.Soft), prepare.Target);
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

        // The branch pipeline (BranchReducer, BranchEffectHandler) is tested on its own: here the list closes and the
        // name prompt of the branch feature opens at the entry.
        Assert.False(harness.Reflog.IsOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Equal($"At {first[..7]}", harness.Host.State.Shell.Prompt!.Context);
        var answer = Assert.IsType<BranchNameAnswer>(harness.Host.State.Shell.Prompt.OnAccepted);
        Assert.Equal(first, answer.StartPoint);
        Assert.True(answer.Checkout);
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

        Assert.False(harness.Reflog.IsOpen);
        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(harness.Effects.OfType<BranchEffect>()));
        Assert.Equal(SwitchTargetKind.DetachedCommit, effect.TargetKind);
        Assert.Equal(first, effect.Target);
    }

    [Fact]
    public async Task GivenABranchOperationRunning_WhenCheckingAnEntryOut_ThenTheListStaysOpenAndNothingRuns()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();
        harness.Host.Dispatch(new BranchDeleteConfirmed(harness.TabId, "topic", repository.Head, null));
        harness.Effects.Clear();

        harness.Run(ActionIds.BranchSwitch);

        Assert.True(harness.Reflog.IsOpen);
        Assert.Empty(harness.Effects.OfType<BranchEffect>());
        Assert.Equal(BranchRefusals.BusyNotice, harness.Statuses.Log.Latest!.Text);
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

        Assert.True(harness.Click(row + 1));
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

        Assert.False(harness.Click(harness.ActionLine(0) + 5));
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

        Assert.False(harness.Click(harness.ActionLine(1)));
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

        Assert.True(harness.Click(harness.ActionLine(0)));

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

        Assert.True(harness.Click(harness.ActionLine(1)));

        Assert.False(harness.Reflog.IsOpen);
        await harness.PumpUntilAsync(() => harness.Mode == KeyMode.Insert);
    }

    [Fact]
    public async Task GivenAnEntry_WhenClickingTheBranchRow_ThenTheBranchPromptOpens()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        await harness.OpenAsync();

        Assert.True(harness.Click(harness.ActionLine(2)));

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

        Assert.True(harness.Click(harness.ActionLine(3)));

        Assert.False(harness.Reflog.IsOpen);
        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(harness.Effects.OfType<BranchEffect>()));
        Assert.Equal(first, effect.Target);
        Assert.False(harness.Click(0));
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
        private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
        private readonly Dispatcher _host;
        private readonly ReflogEffectHandler _handler;
        private readonly ReflogEntryStarter _starter;
        private TerminalTabSession? _session;

        private Harness(IReflogReader? reader)
        {
            _host = new Dispatcher(AppState.Empty, _pump);
            Statuses = new StatusReporter(_host);
            Reset = new ResetActions(_host, new FixedSelection(this));
            _handler = new ReflogEffectHandler(reader ?? TestGit.Services.Reflog, _host);
            _starter = new ReflogEntryStarter(_host, Reset, new RecordingGraph(this));
            var input = new ReflogInput(_host, new FixedSelection(this), GraphGlyphSet.Unicode);
            Reflog = new ReflogFacade(_host, input);
            _host.EffectRaised += Effects.Add;
            _host.EffectRaised += Route;
            _host.Dispatch(new ScreenResizedCommand(new ScreenExtent(100, 30), _host.Clock.GetUtcNow()));
        }

        public ReflogFacade Reflog { get; }

        public ResetActions Reset { get; }

        public Dispatcher Host => _host;

        public List<AppEffect> Effects { get; } = [];

        public TabId TabId => _host.Workspace.ActiveTabId!.Value;

        public StatusReporter Statuses { get; }

        public KeyMode Mode => _host.State.Shell.Mode;

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
            harness._session = new TerminalTabSession(
                new TabSessionParts(
                    tabId,
                    new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)),
                GraphGlyphSet.Unicode);
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                harness._session,
                harness._host,
                harness.Statuses,
                InlineUi.Instance,
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

        /// <summary>A click through the real path: the controller names the view, the mouse map the action.</summary>
        public bool Click(int frameLine)
        {
            var view = Reflog.OnLineClicked(frameLine, out var handled);
            if (view is not null)
            {
                var binding = DefaultMouseMap.Create().Resolve(view, MouseGesture.Click);
                Assert.NotNull(binding);
                Assert.True(Reflog.TryExecute(binding.Action, null), binding.Action.Value);
            }

            return handled;
        }

        // The frame line of the nth action row under the entries.
        public int ActionLine(int index) => 1 + Reflog.State!.Entries.Count + index;

        public void Run(ActionId action) => Assert.True(Reflog.TryExecute(action, null), action.Value);

        public Task PumpUntilAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose() => _session?.Dispose();

        // What the effect runner does with the effects of the reflog and of the controllers that have not moved.
        private void Route(AppEffect effect)
        {
            switch (effect)
            {
                case ReadReflogEffect read:
                    _handler.Handle(read, TestContext.Current.CancellationToken);
                    break;
                case ReflogEntryActionEffect entry:
                    _starter.Start(entry);
                    break;
                case SetKeyModeEffect mode:
                    _host.Dispatch(new SetKeyModeCommand(mode.Mode));
                    break;
                case PostStatusEffect post:
                    _host.Dispatch(new StatusPostedCommand(post.Text, post.Severity, _host.Clock.GetUtcNow()));
                    break;
                case RunActionEffect run:
                    run.Run();
                    break;
                case ShowPromptEffect prompt:
                    _host.Dispatch(new PromptRequestedCommand(prompt.Request));
                    break;
            }
        }

        private sealed class RecordingGraph(Harness owner) : IGraphCommitSelector
        {
            public string SelectCommit(TabId tabId, string hash)
            {
                owner.SelectedCommits.Add(hash);
                return owner.SelectRefusal;
            }
        }

        private sealed class FixedSelection(Harness owner) : IBranchSelectionSource
        {
            public BranchSelection Current() => owner.Selection;
        }
    }

    // The list as the tests read it: the input class over the state in the dispatcher.
    private sealed class ReflogFacade(Dispatcher host, ReflogInput input)
    {
        public bool IsOpen => input.IsOpen;

        public ReflogState? State => host.State.Shell.Reflog.List;

        public OverlayFrame? CurrentFrame() => input.CurrentFrame();

        public bool TryExecute(ActionId action, int? count) => input.TryExecute(action, count);

        public bool HandleTextKey(KeyEvent key) => input.HandleTextKey(key);

        public string? OnLineClicked(int frameLine, out bool handled) => input.OnLineClicked(frameLine, out handled);

        public bool SelectEntryAt(int frameLine)
        {
            input.OnLineClicked(frameLine, out var handled);
            return handled;
        }
    }
}
