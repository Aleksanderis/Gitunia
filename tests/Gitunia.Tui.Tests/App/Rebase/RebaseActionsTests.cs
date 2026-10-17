using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.App.Rebase;

// Real git on temp repositories through the action, the real writer and the real readers.
public class RebaseActionsTests
{
    [Fact]
    public async Task GivenADivergedBranch_WhenRebasingAfterConfirming_ThenItMovesOntoTheTargetAndNamesTheUndo()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        var mainTip = repository.Git("rev-parse", "main").Trim();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(mainTip);

        harness.Execute();

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Rebase", prompt.Title);
        Assert.Contains($"Rebase topic onto {mainTip[..7]}?", prompt.Lines, StringComparer.Ordinal);
        Assert.Contains($"git reset --hard {oldTip}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("ORIG_HEAD", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(oldTip, repository.Head);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(mainTip, repository.Git("rev-parse", "HEAD~1").Trim());
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains($"was {oldTip[..7]}", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Contains($"git reset --hard {oldTip}", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenTheConfirm_WhenDeclined_ThenNothingMovesAndNoGitRuns()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());

        harness.Execute();
        harness.Decline();

        Assert.Equal(oldTip, repository.Head);
        Assert.Null(harness.Statuses.Log.Latest);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenAConflictingBranch_WhenRebasing_ThenARebaseIsInProgressAndAbortReturnsTheOldTip()
    {
        using var repository = TestRepositories.ConflictingBranches().Checkout("other");
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());

        harness.Execute();
        harness.Answer();
        await harness.WaitAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("stopped on conflicts", latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>gc", latest.Text, StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.Rebase, harness.Detect());
        Assert.False(harness.Gate.IsBusy(harness.TabId));

        await harness.ReloadAsync();
        Assert.Equal(RepositoryOperation.Rebase, harness.Tab.Operation!.Kind);

        var aborted = await new OperationWriter(new OperationRunner(harness.Runner, new RepositoryOperationDetector()))
            .AbortAsync(harness.Location, RepositoryOperation.Rebase, TestContext.Current.CancellationToken);
        Assert.Equal(OperationOutcome.Completed, aborted.Outcome);
        Assert.Equal(oldTip, repository.Head);
    }

    [Fact]
    public async Task GivenARebaseInProgress_WhenRebasing_ThenItIsRefusedWithoutAskingOrRunningGit()
    {
        using var repository = TestRepositories.RebaseConflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());

        harness.Execute();

        Assert.Contains("finish or abort the rebase first", harness.Notice, StringComparison.Ordinal);
        Assert.Contains("<space>gc", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
        Assert.Equal(RepositoryOperation.Rebase, harness.Detect());
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenRebasing_ThenItIsRefusedNamingTheMerge()
    {
        using var repository = TestRepositories.Conflicted();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "HEAD~1").Trim());

        harness.Execute();

        Assert.Contains("finish or abort the merge first", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTrackedChanges_WhenRebasing_ThenItIsRefusedCountingTheFilesAndUntrackedFilesAreIgnored()
    {
        using var repository = Diverged();
        var mainTip = repository.Git("rev-parse", "main").Trim();
        repository.WriteFile("untracked.txt", "new");
        using var untracked = await Harness.CreateAsync(repository);
        untracked.Select(mainTip);

        untracked.Execute();

        Assert.Single(untracked.Prompts);
        Assert.Equal(string.Empty, untracked.Notice);

        repository.WriteFile("topic.txt", "edited");
        await untracked.ReloadAsync();
        untracked.Prompts.Clear();
        untracked.Execute();

        Assert.Equal(
            "1 file with uncommitted changes: commit or stash them before a rebase",
            untracked.Notice);
        Assert.Empty(untracked.Prompts);
    }

    [Fact]
    public async Task GivenADetachedHead_WhenRebasing_ThenItIsRefused()
    {
        using var repository = Diverged();
        var mainTip = repository.Git("rev-parse", "main").Trim();
        repository.Checkout(repository.Head);
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(mainTip);

        harness.Execute();

        Assert.Contains("HEAD is detached", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTheCurrentCommit_WhenRebasingOntoIt_ThenItIsRefused()
    {
        using var repository = Diverged();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Head);

        harness.Execute();

        Assert.Contains("already at that commit", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public async Task GivenASidebarRowThatIsNotARef_WhenRebasing_ThenNothingIsAsked(SidebarRowKind kind)
    {
        using var repository = Diverged();
        using var harness = await Harness.CreateAsync(repository);
        harness.SelectRow(kind, repository.Git("rev-parse", "main").Trim());

        harness.Execute();

        Assert.Contains("Select a commit", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenNothingSelected_WhenRebasing_ThenNothingIsAsked()
    {
        using var repository = Diverged();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(null);

        harness.Execute();

        Assert.Contains("Select a commit", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenABranchRow_WhenRebasing_ThenTheConfirmNamesTheBranch()
    {
        using var repository = Diverged();
        using var harness = await Harness.CreateAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, repository.Git("rev-parse", "main").Trim(), "main");

        harness.Execute();

        Assert.Contains("Rebase topic onto main?", Assert.Single(harness.Prompts).Lines);
    }

    [Fact]
    public async Task GivenABranchWithAnUpstream_WhenRebasing_ThenTheConfirmWarnsAboutTheForcePush()
    {
        using var repository = TestRepositories.WithTrackingBranch(ahead: 0, behind: 0);
        repository.Checkout("main").Commit("Main moves", new TestFile("main.txt", "m\n"));
        var topic = repository.Git("rev-parse", "topic").Trim();
        repository.Checkout("topic");
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());

        harness.Execute();

        var lines = string.Join('\n', Assert.Single(harness.Prompts).Lines);
        Assert.Contains("All of its commits are already on origin/topic", lines, StringComparison.Ordinal);
        Assert.Contains("force push is needed", lines, StringComparison.Ordinal);
        Assert.Equal(topic, repository.Head);
    }

    [Fact]
    public async Task GivenABranchWithoutAnUpstream_WhenRebasing_ThenTheConfirmHasNoForcePushWarning()
    {
        using var repository = Diverged();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());

        harness.Execute();

        Assert.DoesNotContain(
            "force push",
            string.Join('\n', Assert.Single(harness.Prompts).Lines),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnotherOperationOfTheTab_WhenRebasing_ThenItIsRefusedAsBusy()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());
        harness.Gate.Begin(harness.TabId);

        harness.Execute();

        Assert.Contains("Busy", harness.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
        Assert.Equal(oldTip, repository.Head);
    }

    [Fact]
    public async Task GivenTheTabBecameBusyWhileAsking_WhenConfirming_ThenNothingIsRebased()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());
        harness.Execute();
        harness.Gate.Begin(harness.TabId);

        harness.Answer();

        Assert.Equal(oldTip, repository.Head);
        Assert.Contains("nothing was rebased", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheDialogCannotShow_WhenRebasing_ThenTheTooSmallNoticeIsGiven()
    {
        using var repository = Diverged();
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());
        harness.CanShowDialog = false;

        harness.Execute();

        Assert.Contains("too small", harness.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFailingRebase_WhenConfirmed_ThenGitsReasonIsShownAndTheGateIsReleased()
    {
        using var repository = Diverged();
        var oldTip = repository.Head;
        using var harness = await Harness.CreateAsync(repository);
        harness.Select(repository.Git("rev-parse", "main").Trim());
        harness.Execute();
        repository.WriteFile("topic.txt", "edited after the tab loaded");

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not rebase topic onto", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(oldTip, repository.Head);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("topic")
            .Commit("Topic change", new TestFile("topic.txt", "topic\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("main.txt", "main\n"))
            .Checkout("topic");

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];
        private BranchSelection _selection = new(null, null, null);
        private readonly TabId _tabId;

        private Harness(TestRepository repository, RepositoryLocation location)
        {
            Runner = repository.CreateIsolatedRunner();
            Location = location;
            _host.Dispatch(new OpenRepositoryCommand(location));
            _tabId = _host.Workspace.ActiveTabId!.Value;
            Pump = new UiPump(TestContext.Current.CancellationToken);
            var writer = new RebaseWriter(new OperationRunner(Runner, new RepositoryOperationDetector()));
            Actions = new RebaseActions(
                _host,
                writer,
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
                Gate);
        }

        public GitRunner Runner { get; }

        public RepositoryLocation Location { get; }

        public RebaseActions Actions { get; }

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public int Refreshes { get; private set; }

        public bool CanShowDialog { get; set; } = true;

        public string Notice => Actions.Notice;

        public TabId TabId => _tabId;

        public RepositoryState Tab => _host.Workspace.FindTab(_tabId)!;

        private UiPump Pump { get; }

        public static async Task<Harness> CreateAsync(TestRepository repository)
        {
            var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
            var location = await discovery.DiscoverAsync(repository.WorkingTreePath, TestContext.Current.CancellationToken);
            var harness = new Harness(repository, location);
            await harness.ReloadAsync();
            return harness;
        }

        // What the tab refresher does after a change: head, refs, status and operation details.
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

        public void Select(string? commitHash) => _selection = new BranchSelection(null, null, commitHash);

        public void SelectRow(SidebarRowKind kind, string commitHash, string name = "row") =>
            _selection = new BranchSelection(kind, name, commitHash);

        public void Execute() => Assert.True(Actions.TryExecute(ActionIds.RebaseOnto, null));

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

        private void Send(RepositoryCommand command) => _host.Dispatch(new TabCommand(_tabId, command));
    }
}
