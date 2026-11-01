using Gitunia.Core;
using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Rebase;

// Real git on temp repositories through the controller, the real engine (cp as the sequence editor) and real readers.
public class RebaseEditorControllerTests
{
    [Fact]
    public async Task GivenABranchAheadOfTheTarget_WhenOpeningTheEditor_ThenTheCommitsAreListedOldestFirstAsPicks()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);

        await harness.OpenAsync();

        var state = harness.Controller.State!;
        Assert.Equal(["add a", "add b", "add c"], state.Rows.Select(row => row.Subject));
        Assert.All(state.Rows, row => Assert.Equal(RebaseTodoAction.Pick, row.Action));
        Assert.Equal("topic", state.BranchName);
        Assert.Equal(harness.Tab.Head!.CommitHash, state.OldTip);
        Assert.NotNull(harness.Controller.CurrentFrame());
    }

    [Fact]
    public async Task GivenNoSelection_WhenOpeningTheRootEditor_ThenTheWholeHistoryIsListedAndRewordingTheRootWorks()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository, target: null);

        await harness.OpenRootAsync();

        var state = harness.Controller.State!;
        Assert.True(state.Base.IsRoot);
        Assert.Equal(["Initial", "add a", "add b", "add c"], state.Rows.Select(row => row.Subject));
        await harness.RewordAsync("root reworded");
        harness.Run(ActionIds.RebaseEditorStart);
        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("Rebase topic onto the root with this todo?", prompt.Lines, StringComparer.Ordinal);
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(
            ["root reworded", "add a", "add b", "add c"],
            repository.Git("log", "--format=%s", "--reverse").Trim().Split('\n'));
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest!.Severity);
    }

    [Fact]
    public async Task GivenUncommittedChanges_WhenOpeningTheRootEditor_ThenItIsRefusedLikeTheOtherRebase()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository, target: null);
        File.WriteAllText(Path.Combine(repository.WorkingTreePath, "a.txt"), "dirty\n");
        await harness.ReloadAsync();

        Assert.True(harness.Controller.TryExecute(ActionIds.RebaseInteractiveRoot, null));

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("uncommitted changes", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenReorderFixupAndReword_WhenConfirmed_ThenGitReplaysTheTodoAndTheStatusNamesTheUndo()
    {
        using var repository = FourCommits();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        // Rows: a, b, c, d. Move c above b, fix d up into b, drop nothing, reword a.
        harness.Run(ActionIds.ListDown, 2);
        harness.Run(ActionIds.RebaseEditorMoveUp);
        harness.Run(ActionIds.ListBottom);
        harness.Run(ActionIds.RebaseEditorFixup);
        harness.Run(ActionIds.ListTop);
        await harness.RewordAsync("a reworded\n\nwith a body");
        harness.Run(ActionIds.RebaseEditorStart);
        harness.Answer();
        await harness.WaitAsync();

        Assert.False(harness.Controller.IsOpen);
        Assert.Equal(["a reworded", "add c", "add b"], Subjects(repository));
        Assert.Equal("with a body", repository.Git("log", "-1", "--format=%b", "HEAD~2").Trim());
        Assert.Equal("d\n", repository.Git("show", "HEAD:d.txt").Replace("\r", string.Empty));
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains($"git reset --keep {oldTip}", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
        Assert.Equal(RepositoryOperation.None, harness.Detect());
    }

    [Fact]
    public async Task GivenADroppedCommit_WhenStarting_ThenTheConfirmNamesItAndTheCommitIsGoneAfterTheRebase()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ListDown);
        harness.Run(ActionIds.RebaseEditorDrop);

        harness.Run(ActionIds.RebaseEditorStart);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Interactive rebase", prompt.Title);
        var targetLabel = repository.Git("rev-parse", "main").Trim()[..7];
        Assert.Contains($"Rebase topic onto {targetLabel} with this todo?", prompt.Lines, StringComparer.Ordinal);
        Assert.Contains("2 of 3 commits kept, 1 dropped", prompt.Lines, StringComparer.Ordinal);
        Assert.Contains(prompt.Lines, line => line.Contains("add b", StringComparison.Ordinal));
        Assert.Contains($"git reset --keep {oldTip}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("ORIG_HEAD", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(oldTip, repository.Head);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["add a", "add c"], Subjects(repository));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenTheConfirm_WhenDeclined_ThenTheEditorStaysOpenWithItsTodoAndNothingChanged()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorDrop);
        harness.Run(ActionIds.RebaseEditorStart);

        harness.Decline();

        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(RebaseTodoAction.Drop, harness.Controller.State!.Rows[0].Action);
        Assert.Equal(oldTip, repository.Head);
        Assert.Equal(0, harness.Refreshes);
        Assert.Null(harness.Statuses.Log.Latest);
    }

    [Fact]
    public async Task GivenAFirstKeptSquash_WhenStarting_ThenNoConfirmOpensAndTheStatusNamesTheRule()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorSquash);

        harness.Run(ActionIds.RebaseEditorStart);

        Assert.Empty(harness.Prompts);
        Assert.Contains("cannot be a squash or a fixup", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest.Severity);
        Assert.True(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenAConflictingReorder_WhenStarting_ThenARebaseIsInProgressAndTheStatusNamesTheKeys()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("f.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("first", new TestFile("f.txt", "first\n"))
            .Commit("second", new TestFile("f.txt", "second\n"));
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository, target: repository.Git("rev-parse", "main").Trim());
        await harness.OpenAsync();
        harness.Run(ActionIds.ListDown);
        harness.Run(ActionIds.RebaseEditorMoveUp);

        harness.Run(ActionIds.RebaseEditorStart);
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(RepositoryOperation.Rebase, harness.Detect());
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("<space>gc", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Contains(oldTip[..7], harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.False(harness.Controller.IsOpen);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenARangeWithAMergeCommit_WhenOpening_ThenTheEditorOpensWithTheMergeAndItsStructureLines()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("topic one", new TestFile("one.txt", "1\n"))
            .Checkout("main")
            .CheckoutNewBranch("side")
            .Commit("side", new TestFile("side.txt", "s\n"))
            .Checkout("topic")
            .Merge("side");
        using var harness = await Harness.CreateAsync(repository, target: repository.Git("rev-parse", "main").Trim());

        harness.Open();
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsOpen);

        var rows = harness.Controller.State!.Rows;
        Assert.Contains(
            rows,
            row => row.Action == RebaseTodoAction.Merge && row.Subject.StartsWith("Merge", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Action == RebaseTodoAction.Label);
        Assert.True(harness.Controller.State.Base.KeepMerges);
    }

    [Fact]
    public async Task GivenATargetTheBranchAlreadyHas_WhenOpening_ThenNoEditorOpensAndTheStatusSaysSo()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository, target: repository.Head);

        harness.Open();

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("nothing to rebase", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTrackedChanges_WhenOpening_ThenNoGitReadsHappenAndTheStatusRefuses()
    {
        using var repository = ThreeCommits();
        repository.WriteFile("a.txt", "dirty\n");
        using var harness = await Harness.CreateAsync(repository);

        harness.Open();

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("uncommitted changes", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoSelectionTarget_WhenOpening_ThenTheStatusAsksForOne()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository, target: null);

        harness.Open();

        Assert.False(harness.Controller.IsOpen);
        Assert.Contains("Select a commit", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenASidebarBranchAsTarget_WhenOpening_ThenTheTitleNamesTheBranch()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, repository.Git("rev-parse", "main").Trim(), "main");

        await harness.OpenAsync();

        Assert.Equal("main", harness.Controller.State!.TargetLabel);
        Assert.Contains("onto main", harness.Controller.CurrentFrame()!.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnEditedTodo_WhenPressingEscTwice_ThenTheFirstAsksAndTheSecondCloses()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        harness.Run(ActionIds.NavigateBack);
        Assert.False(harness.Controller.IsOpen);

        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorDrop);
        harness.Run(ActionIds.NavigateBack);
        Assert.True(harness.Controller.IsOpen);
        Assert.True(harness.Controller.State!.IsDiscardPending);
        harness.Run(ActionIds.NavigateBack);

        Assert.False(harness.Controller.IsOpen);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenACommitWithABody_WhenRewording_ThenTheEditorOpensWithTheWholeMessageInInsertMode()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("subject line\n\nbody paragraph", new TestFile("a.txt", "a\n"));
        using var harness = await Harness.CreateAsync(repository, target: repository.Git("rev-parse", "main").Trim());
        await harness.OpenAsync();

        harness.Run(ActionIds.RebaseEditorReword);
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsEditingMessage);

        Assert.Equal("subject line\n\nbody paragraph", harness.Controller.State!.Edit!.Editor.Text);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        var frameText = string.Concat(harness.Controller.CurrentFrame()!.Lines.Select(line => line.Text));
        Assert.Contains("body paragraph", frameText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheMessageEditor_WhenTypingEnterAndSaving_ThenTheCommitIsAWordedRewordAndNormalModeIsBack()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorReword);
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsEditingMessage);
        harness.Controller.State!.Edit!.Editor.SetText(string.Empty);

        Assert.True(harness.Controller.HandleTextKey(new KeyEvent("n")));
        harness.Run(ActionIds.RebaseEditorMessageNewLine);
        Assert.True(harness.Controller.HandleTextKey(new KeyEvent("x")));
        harness.Run(ActionIds.RebaseEditorMessageSave);

        Assert.False(harness.Controller.IsEditingMessage);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(RebaseTodoAction.Reword, harness.Controller.State!.Rows[0].Action);
        Assert.Equal("n\nx", harness.Controller.State.Rows[0].Message);
    }

    [Fact]
    public async Task GivenABlankMessage_WhenSaving_ThenTheEditorStaysOpenWithAWarning()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorReword);
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsEditingMessage);
        harness.Controller.State!.Edit!.Editor.SetText(string.Empty);

        harness.Run(ActionIds.RebaseEditorMessageSave);

        Assert.True(harness.Controller.IsEditingMessage);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Contains("needs a message", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheMessageEditor_WhenCancelling_ThenTheListIsBackUnchangedInNormalMode()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorReword);
        await harness.Pump.RunUntilAsync(() => harness.Controller.IsEditingMessage);
        harness.Controller.HandleTextKey(new KeyEvent("z"));

        harness.Run(ActionIds.Cancel);

        Assert.True(harness.Controller.IsOpen);
        Assert.False(harness.Controller.IsEditingMessage);
        Assert.False(harness.Controller.State!.IsModified);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenTextKeysWithoutAMessageEditor_WhenHandlingThem_ThenTheyAreNotTaken()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        Assert.False(harness.Controller.HandleTextKey(new KeyEvent("a")));
        await harness.OpenAsync();

        Assert.False(harness.Controller.HandleTextKey(new KeyEvent("a")));
    }

    [Fact]
    public async Task GivenTheEditorOpen_WhenAnotherActionArrives_ThenItIsConsumedAndNothingBehindMoves()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        Assert.True(harness.Controller.TryExecute(ActionIds.PaneNext, null));
        Assert.True(harness.Controller.TryExecute(ActionIds.HelpShow, null));
        Assert.True(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenADialogOverTheEditor_WhenActionsAndFramesAreRequested_ThenTheEditorStepsAside()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.IsOtherOverlayOpen = true;

        Assert.False(harness.Controller.TryExecute(ActionIds.ConfirmYes, null));
        Assert.Null(harness.Controller.CurrentFrame());
        Assert.Null(harness.Controller.OnLineClicked(2, 5, out var handled));
        Assert.False(handled);

        harness.IsOtherOverlayOpen = false;
        Assert.NotNull(harness.Controller.CurrentFrame());
        Assert.True(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenAnotherOverlayOpen_WhenAskingForTheEditor_ThenItDoesNotOpen()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        harness.IsOtherOverlayOpen = true;

        harness.Open();

        Assert.False(harness.Controller.IsOpen);
        Assert.Null(harness.Statuses.Log.Latest);
    }

    [Fact]
    public async Task GivenClicks_WhenClickingACommitRowAndAChip_ThenTheRowIsHighlightedAndTheChipNamesItsView()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();

        Assert.Null(harness.Controller.OnLineClicked(4, 10, out var rowHandled));
        Assert.True(rowHandled);
        Assert.Equal(2, harness.Controller.State!.SelectedIndex);

        var frame = harness.Controller.CurrentFrame()!;
        var chipLine = frame.Lines.ToList().FindIndex(line => line.Text.Contains("p pick", StringComparison.Ordinal));
        var column = frame.Lines[chipLine].Text.IndexOf("d drop", StringComparison.Ordinal);
        var view = harness.Controller.OnLineClicked(chipLine, column, out var chipHandled);
        Assert.Equal(DefaultMouseMap.RebaseDropButton, view);
        Assert.True(chipHandled);

        Assert.Null(harness.Controller.OnLineClicked(0, 3, out var titleHandled));
        Assert.False(titleHandled);
    }

    [Fact]
    public async Task GivenACount_WhenMovingUp_ThenTheCommitMovesThatManyRows()
    {
        using var repository = FourCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.ListBottom);

        harness.Run(ActionIds.RebaseEditorMoveUp, 2);

        Assert.Equal(["add a", "add d", "add b", "add c"], harness.Controller.State!.Rows.Select(row => row.Subject));
        Assert.Equal(1, harness.Controller.State.SelectedIndex);
    }

    [Fact]
    public async Task GivenHeadMovedWhileTheTodoIsOpen_WhenConfirming_ThenNothingIsRebasedAndTheTodoStays()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorDrop);
        harness.Run(ActionIds.RebaseEditorStart);
        repository.Commit("elsewhere", new TestFile("e.txt", "e\n"));
        var movedTip = repository.Head;
        await harness.ReloadAsync();

        harness.Answer();

        Assert.Equal(movedTip, repository.Head);
        Assert.Contains("changed while the todo was open", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.True(harness.Controller.IsOpen);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenGitSequenceEditorIsSet_WhenStarting_ThenTheStatusExplainsAndTheTodoComesBack()
    {
        using var repository = ThreeCommits();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(
            repository,
            environment: name => name == "GIT_SEQUENCE_EDITOR" ? "vim" : null);
        await harness.OpenAsync();
        harness.Run(ActionIds.RebaseEditorDrop);
        harness.Run(ActionIds.RebaseEditorStart);

        harness.Answer();
        await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Contains("GIT_SEQUENCE_EDITOR", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest.Severity);
        Assert.True(harness.Controller.IsOpen);
        Assert.Equal(RebaseTodoAction.Drop, harness.Controller.State!.Rows[0].Action);
        Assert.Equal(oldTip, repository.Head);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenTheTabWasClosed_WhenAnActionArrives_ThenTheEditorClosesAndDoesNotConsumeIt()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.CloseTab();

        Assert.False(harness.Controller.TryExecute(ActionIds.RebaseEditorDrop, null));

        Assert.False(harness.Controller.IsOpen);
    }

    [Fact]
    public async Task GivenTheConfirmCannotShow_WhenStarting_ThenTheStatusSaysNothingWasRebasedAndTheTodoStays()
    {
        using var repository = ThreeCommits();
        using var harness = await Harness.CreateAsync(repository);
        await harness.OpenAsync();
        harness.CanShowDialog = false;

        harness.Run(ActionIds.RebaseEditorStart);

        Assert.Contains("too small", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.True(harness.Controller.IsOpen);
    }

    // main has "Initial"; topic adds a.txt, b.txt, c.txt in three commits.
    private static TestRepository ThreeCommits() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("add a", new TestFile("a.txt", "a\n"))
            .Commit("add b", new TestFile("b.txt", "b\n"))
            .Commit("add c", new TestFile("c.txt", "c\n"));

    private static IEnumerable<string> Subjects(TestRepository repository) =>
        repository.Git("log", "--format=%s", "main..HEAD").Trim().Split('\n').Reverse();

    private static TestRepository FourCommits() =>
        ThreeCommits().Commit("add d", new TestFile("d.txt", "d\n"));

    private sealed class Harness : IDisposable
    {
        private readonly Dispatcher _host = new(AppState.Empty);
        private readonly List<Action> _answers = [];
        private readonly List<KeyMode> _modes = [];
        private BranchSelection _selection = new(null, null, null);

        private Harness(TestRepository repository, RepositoryLocation location, Func<string, string?> environment)
        {
            Runner = repository.CreateIsolatedRunner();
            Location = location;
            _host.Dispatch(new OpenRepositoryCommand(location));
            TabId = _host.Workspace.ActiveTabId!.Value;
            Pump = new UiPump(TestContext.Current.CancellationToken);
            var detector = new RepositoryOperationDetector();
            var writer = new InteractiveRebaseWriter(
                new OperationRunner(Runner, detector),
                Runner,
                detector,
                new SequenceEditorCommand(["cp"]),
                environment);
            Controller = new RebaseEditorController(
                _host,
                writer,
                new CommitDetailsReader(Runner),
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                () => _selection,
                () => IsOtherOverlayOpen,
                () => new ScreenSize(100, 30),
                GraphGlyphSet.Unicode,
                _modes.Add,
                () => { },
                Gate);
        }

        public GitRunner Runner { get; }

        public RepositoryLocation Location { get; }

        public RebaseEditorController Controller { get; }

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public int Refreshes { get; private set; }

        public bool CanShowDialog { get; set; } = true;

        public bool IsOtherOverlayOpen { get; set; }

        public TabId TabId { get; }

        public UiPump Pump { get; }

        public KeyMode Mode => _modes.Count == 0 ? KeyMode.Normal : _modes[^1];

        public RepositoryState Tab => _host.Workspace.FindTab(TabId)!;

        // The target is the commit of main unless a test names another (null: nothing selected).
        public static async Task<Harness> CreateAsync(
            TestRepository repository,
            string? target = "main",
            Func<string, string?>? environment = null)
        {
            var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
            var token = TestContext.Current.CancellationToken;
            var location = await discovery.DiscoverAsync(repository.WorkingTreePath, token);
            var harness = new Harness(repository, location, environment ?? (_ => null));
            await harness.ReloadAsync();
            var targetCommit = target;

            if (target == "main")
            {
                targetCommit = repository.Git("rev-parse", "main").Trim();
            }

            harness._selection = new BranchSelection(null, null, targetCommit);
            return harness;
        }

        public async Task ReloadAsync()
        {
            var token = TestContext.Current.CancellationToken;
            var refs = new RefReader(Runner);
            Send(new HeadLoadedCommand(await refs.ReadHeadAsync(Location, token)));
            var read = await refs.ReadRefsAsync(Location, token);
            Send(new RefsLoadedCommand([.. read.Items]));
            var status = await new StatusReader(Runner).ReadStatusAsync(Location, false, UntrackedFilesMode.All, token);
            Send(new WorkingTreeLoadedCommand(status));
            var operation = new RepositoryOperationDetector().Detect(Location);
            Send(new OperationLoadedCommand(await new OperationInfoReader().ReadAsync(Location, operation, token)));
        }

        public RepositoryOperation Detect() => new RepositoryOperationDetector().Detect(Location);

        public void SelectRow(SidebarRowKind kind, string commitHash, string name) =>
            _selection = new BranchSelection(kind, name, commitHash);

        public void CloseTab() => _host.Dispatch(new CloseTabCommand(TabId));

        public void Open() => Assert.True(Controller.TryExecute(ActionIds.RebaseInteractive, null));

        // Opening reads the range on a worker: wait until the editor shows or the status reported a refusal.
        public async Task OpenRootAsync()
        {
            Assert.True(Controller.TryExecute(ActionIds.RebaseInteractiveRoot, null));
            await Pump.RunUntilAsync(() => Controller.IsOpen || Statuses.Log.Latest is not null);
            Assert.True(Controller.IsOpen, Statuses.Log.Latest?.Text);
        }

        public async Task OpenAsync()
        {
            Open();
            await Pump.RunUntilAsync(() => Controller.IsOpen || Statuses.Log.Latest is not null);
            Assert.True(Controller.IsOpen, Statuses.Log.Latest?.Text);
        }

        public void Run(ActionId action, int? count = null) => Assert.True(Controller.TryExecute(action, count));

        // Types a message into the editor of the highlighted commit and keeps it.
        public async Task RewordAsync(string message)
        {
            Run(ActionIds.RebaseEditorReword);
            await Pump.RunUntilAsync(() => Controller.IsEditingMessage);
            Controller.State!.Edit!.Editor.SetText(message);
            Run(ActionIds.RebaseEditorMessageSave);
        }

        public void Answer()
        {
            var answers = _answers.ToList();
            _answers.Clear();
            foreach (var answer in answers)
            {
                answer();
            }
        }

        public void Decline() => _answers.Clear();

        public Task WaitAsync() => Pump.RunUntilAsync(() => !Gate.IsAnyBusy && Statuses.Log.Latest is not null);

        public void Dispose()
        {
        }

        private bool Confirm(ConfirmPrompt prompt, Action onConfirmed)
        {
            if (!CanShowDialog)
            {
                return false;
            }

            Prompts.Add(prompt);
            _answers.Add(onConfirmed);
            return true;
        }

        private void Send(RepositoryCommand command) => _host.Dispatch(new TabCommand(TabId, command));
    }
}
