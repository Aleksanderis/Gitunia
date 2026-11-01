using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Rebase;

// Real git on temp repositories through the action, the real writers and the real readers.
public class QuickRebaseActionsTests
{
    [Fact]
    public async Task GivenAMiddleCommit_WhenDroppedAfterConfirming_ThenItIsGoneAndTheUndoIsNamed()
    {
        using var repository = Linear();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "HEAD~1").Trim());

        await harness.ExecuteAsync(ActionIds.RebaseDrop);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Drop commit", prompt.Title);
        Assert.Contains("\"Three\"", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("1 later commit of main is replayed and gets", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Contains($"git reset --keep {oldTip}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(oldTip, repository.Head);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["One", "Two", "Four"], Subjects(repository));
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains($"git reset --keep {oldTip}", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenACommit_WhenFixedUpAfterConfirming_ThenItMeldsIntoTheCommitBefore()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "HEAD~1").Trim());

        await harness.ExecuteAsync(ActionIds.RebaseFixup);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Fix up commit", prompt.Title);
        Assert.Contains("\"Three\"", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("\"Two\"", prompt.Lines[0], StringComparison.Ordinal);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["One", "Two", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenTheTipCommit_WhenDropping_ThenTheConfirmSaysItIsTheTip()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Head);

        await harness.ExecuteAsync(ActionIds.RebaseDrop);

        Assert.Contains("tip of main", Assert.Single(harness.Prompts).Lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACommit_WhenRewordedWithANewSubject_ThenThePromptThenTheConfirmThenTheMessageChanges()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "HEAD~1").Trim());

        await harness.ExecuteAsync(ActionIds.RebaseReword, waitForPrompt: true);

        Assert.Equal(KeyMode.Insert, harness.Mode);
        var popup = harness.Actions.Popup!;
        Assert.Equal("Reword commit", popup.Title);
        Assert.Equal("Three", popup.Input);
        Assert.Empty(harness.Prompts);

        harness.Type("\u0015Third");
        Assert.True(harness.Actions.TryExecute(ActionIds.InputAccept, null));

        Assert.Equal(KeyMode.Confirm, harness.Mode);
        Assert.Null(harness.Actions.Popup);
        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("\"Third\"", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Equal(["One", "Two", "Three", "Four"], Subjects(repository));

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["One", "Two", "Third", "Four"], Subjects(repository));
    }

    [Fact]
    public async Task GivenTheRewordPrompt_WhenTheSubjectIsUnchangedOrEmpty_ThenItStaysOpenWithAReason()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Head);
        await harness.ExecuteAsync(ActionIds.RebaseReword, waitForPrompt: true);

        Assert.True(harness.Actions.TryExecute(ActionIds.InputAccept, null));
        Assert.Contains("unchanged", harness.Actions.Notice, StringComparison.Ordinal);
        harness.Type("\u0015");
        Assert.True(harness.Actions.TryExecute(ActionIds.InputAccept, null));

        Assert.Contains("empty", harness.Actions.Notice, StringComparison.Ordinal);
        Assert.NotNull(harness.Actions.Popup);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTheRewordPrompt_WhenCancelled_ThenNothingChanges()
    {
        using var repository = Linear();
        var head = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(head);
        await harness.ExecuteAsync(ActionIds.RebaseReword, waitForPrompt: true);

        Assert.True(harness.Actions.TryExecute(ActionIds.Cancel, null));

        Assert.Null(harness.Actions.Popup);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(head, repository.Head);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTheConfirm_WhenDeclined_ThenNothingMoves()
    {
        using var repository = Linear();
        var head = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "HEAD~2").Trim());

        await harness.ExecuteAsync(ActionIds.RebaseDrop);
        harness.Decline();

        Assert.Equal(head, repository.Head);
        Assert.Null(harness.Statuses.Log.Latest);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenADropThatConflicts_WhenConfirmed_ThenARebaseIsInProgressAndTheStatusWarns()
    {
        using var repository = TestRepository.Create()
            .Commit("One", new TestFile("f.txt", "1\n"))
            .Commit("Two", new TestFile("f.txt", "1\n2\n"))
            .Commit("Three", new TestFile("f.txt", "1\n2\n3\n"));
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "HEAD~1").Trim());

        await harness.ExecuteAsync(ActionIds.RebaseDrop);
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(RepositoryOperation.Rebase, harness.Detect());
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("<space>gc", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTrackedChanges_WhenDropping_ThenItIsRefusedBeforeAnyQuestion()
    {
        using var repository = Linear();
        File.WriteAllText(Path.Combine(repository.WorkingTreePath, "one.txt"), "changed\n");
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Head);

        await harness.ExecuteAsync(ActionIds.RebaseDrop);

        Assert.Contains("uncommitted changes", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenADetachedHead_WhenDropping_ThenItIsRefused()
    {
        using var repository = Linear();
        repository.Checkout(repository.Head);
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Head);

        await harness.ExecuteAsync(ActionIds.RebaseDrop);

        Assert.Contains("HEAD is detached", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTheRootCommit_WhenFixingUp_ThenTheReasonIsShownAndNothingIsAsked()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-list", "--max-parents=0", "HEAD").Trim());

        await harness.ExecuteAsync(ActionIds.RebaseFixup, waitForStatus: true);

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("no commit before it", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenNothingSelected_WhenDropping_ThenNothingIsAsked()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);

        await harness.ExecuteAsync(ActionIds.RebaseDrop);

        Assert.Contains("Select a commit", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenAnOverlayOpenedWhileAsking_WhenThePlanArrives_ThenTheConfirmIsDropped()
    {
        using var repository = Linear();
        var head = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(head);

        Assert.True(harness.Actions.TryExecute(ActionIds.RebaseDrop, null));
        harness.IsOverlayOpen = true;
        await harness.WaitForStatusAsync();

        Assert.Empty(harness.Prompts);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        using var repository = Linear();
        using var harness = await Harness.CreateAsync(repository);

        Assert.False(harness.Actions.TryExecute(ActionIds.RebaseOnto, null));
    }

    private static string[] Subjects(TestRepository repository) =>
        repository.Git("log", "--reverse", "--format=%s").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static TestRepository Linear() =>
        TestRepository.Create()
            .Commit("One", new TestFile("one.txt", "1\n"))
            .Commit("Two", new TestFile("two.txt", "2\n"))
            .Commit("Three", new TestFile("three.txt", "3\n"))
            .Commit("Four", new TestFile("four.txt", "4\n"));

    private sealed class Harness : IDisposable
    {
        private readonly TestShell _shell = new();
        private string? _selection;

        private Harness(TestRepository repository, RepositoryLocation location)
        {
            Runner = repository.CreateIsolatedRunner();
            Location = location;
            _shell.Host.Dispatch(new OpenRepositoryCommand(location));
            TabId = _shell.Host.Workspace.ActiveTabId!.Value;
            Pump = new UiPump(TestContext.Current.CancellationToken);
            var detector = new RepositoryOperationDetector();
            var interactive = new InteractiveRebaseWriter(
                new OperationRunner(Runner, detector),
                Runner,
                detector,
                new SequenceEditorCommand(["cp"]),
                _ => null);
            Actions = new QuickRebaseActions(
                _shell.Host,
                new QuickRebaseWriter(Runner, interactive),
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                () => _selection,
                () => IsOverlayOpen,
                () => { },
                Gate);
        }

        public GitRunner Runner { get; }

        public RepositoryLocation Location { get; }

        public QuickRebaseActions Actions { get; }

        public StatusReporter Statuses => _shell.Statuses;

        public TabOperationGate Gate { get; } = new();

        public List<ConfirmPrompt> Prompts => _shell.Prompts;

        public int Refreshes { get; private set; }

        public bool IsOverlayOpen { get; set; }

        public KeyMode Mode => _shell.Mode;

        public string Notice => Actions.Notice;

        public TabId TabId { get; }

        private UiPump Pump { get; }

        public static async Task<Harness> CreateAsync(TestRepository repository)
        {
            var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
            var token = TestContext.Current.CancellationToken;
            var location = await discovery.DiscoverAsync(repository.WorkingTreePath, token);
            var harness = new Harness(repository, location);
            await harness.ReloadAsync();
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

        public void Select(string? commitHash) => _selection = commitHash;

        public Task WaitForStatusAsync() => Pump.RunUntilAsync(() => Statuses.Log.Latest is not null);

        // The question opens once the plan is read, so wait for it unless the action refused at once.
        public async Task ExecuteAsync(ActionId action, bool waitForPrompt = false, bool waitForStatus = false)
        {
            Assert.True(Actions.TryExecute(action, null));
            if (Notice.Length != 0)
            {
                return;
            }

            await Pump.RunUntilAsync(() =>
                Prompts.Count > 0 || Statuses.Log.Latest is not null || (waitForPrompt && Actions.Popup is not null));
            Assert.True(!waitForStatus || Statuses.Log.Latest is not null);
        }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                var key = new KeyEvent(character.ToString());

                if (character == '\u0015')
                {
                    key = new KeyEvent("u", KeyModifiers.Ctrl);
                }

                Assert.True(Actions.HandleTextKey(key));
            }
        }
        public void Answer() => _shell.Answer();


        public void Decline() => _shell.Decline();

        public Task WaitAsync() => Pump.RunUntilAsync(() => !Gate.IsAnyBusy && Statuses.Log.Latest is not null);

        public void Dispose()
        {
        }
        private void Send(RepositoryCommand command) => _shell.Host.Dispatch(new TabCommand(TabId, command));
    }
}
