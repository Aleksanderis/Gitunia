using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Merge;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Merge;

public class MergeActionsTests
{
    [Fact]
    public async Task GivenABranchRow_WhenMerging_ThenItFastForwardsAndNamesTheOldTipForTheUndo()
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        var topic = repository.Git("rev-parse", "topic").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, "topic", topic);

        harness.Run(ActionIds.MergeBranch);
        await harness.FinishAsync();

        Assert.Equal(topic, repository.Head);
        var text = harness.Statuses.Log.Latest!.Text;
        Assert.StartsWith("Fast-forwarded main", text, StringComparison.Ordinal);
        Assert.Contains($"git reset --hard {old}", text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenAGraphCommit_WhenMergingWithNoFastForward_ThenAMergeCommitIsMade()
    {
        using var repository = AheadBranch();
        var topic = repository.Git("rev-parse", "topic").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(topic);

        harness.Run(ActionIds.MergeBranchNoFastForward);
        await harness.FinishAsync();

        Assert.Equal(2, repository.Git("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Length - 1);
        Assert.StartsWith("Merged ", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABranch_WhenSquashing_ThenTheChangesAreStagedAndTheStatusSaysNothingIsCommitted()
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, "topic", repository.Git("rev-parse", "topic").Trim());

        harness.Run(ActionIds.MergeBranchSquash);
        await harness.FinishAsync();

        Assert.Equal(old, repository.Head);
        Assert.Contains("A  b.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
        Assert.Contains("nothing is committed", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenConflictingBranches_WhenMerging_ThenAWarningPointsToContinueAndAbort()
    {
        using var repository = TestRepositories.ConflictingBranches();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, "other", repository.Git("rev-parse", "other").Trim());

        harness.Run(ActionIds.MergeBranch);
        await harness.FinishAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("<space>gc", latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>ga", latest.Text, StringComparison.Ordinal);
        Assert.Contains("UU conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenConflictingBranches_WhenSquashing_ThenTheWarningSaysNoOperationIsInProgress()
    {
        using var repository = TestRepositories.ConflictingBranches();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, "other", repository.Git("rev-parse", "other").Trim());

        harness.Run(ActionIds.MergeBranchSquash);
        await harness.FinishAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("no operation is in progress", latest.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("conflicted")]
    [InlineData("rebase")]
    [InlineData("cherry-pick")]
    [InlineData("revert")]
    public async Task GivenAnOperationInProgress_WhenMerging_ThenItIsRefusedByNameAndNothingRuns(string kind)
    {
        using var repository = kind switch
        {
            "conflicted" => TestRepositories.Conflicted(),
            "rebase" => TestRepositories.RebaseConflicted(),
            "cherry-pick" => TestRepositories.CherryPickConflicted(),
            _ => TestRepositories.RevertConflicted(),
        };
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(head);

        harness.Run(ActionIds.MergeBranch);

        var name = kind == "conflicted" ? "merge" : kind;
        Assert.Contains($"finish or abort the {name} first", harness.Merge.Notice, StringComparison.Ordinal);
        Assert.Contains("<space>gc", harness.Merge.Notice, StringComparison.Ordinal);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
        Assert.Equal(0, harness.Refreshes);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenADetachedHead_WhenMerging_ThenItIsRefused()
    {
        using var repository = AheadBranch();
        repository.Checkout(repository.Head);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(repository.Git("rev-parse", "topic").Trim());

        harness.Run(ActionIds.MergeBranch);

        Assert.Contains("detached", harness.Merge.Notice, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public async Task GivenASidebarRowThatIsNotARef_WhenMerging_ThenNothingRuns(SidebarRowKind kind)
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(kind, "row", repository.Git("rev-parse", "topic").Trim());

        harness.Run(ActionIds.MergeBranch);

        Assert.Contains("Select a commit", harness.Merge.Notice, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenATagRow_WhenMerging_ThenTheTagCommitIsMerged()
    {
        using var repository = AheadBranch();
        repository.Git("tag", "v1", "topic");
        var topic = repository.Git("rev-parse", "topic").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.Tag, "v1", topic);

        harness.Run(ActionIds.MergeBranch);
        await harness.FinishAsync();

        Assert.Equal(topic, repository.Head);
    }

    [Fact]
    public async Task GivenAnotherOperationOfTheTab_WhenMerging_ThenItIsBusyAndTheGateIsKept()
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, "topic", repository.Git("rev-parse", "topic").Trim());
        harness.Gate.Begin(harness.TabId);

        harness.Run(ActionIds.MergeBranch);

        Assert.Contains("Busy", harness.Merge.Notice, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
        Assert.True(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenADirtyTreeGitCannotMergeOver_WhenMerging_ThenGitsMessageIsShownAsAnError()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("a.txt", "topic"))
            .Checkout("main")
            .WriteFile("a.txt", "dirty");
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, "topic", repository.Git("rev-parse", "topic").Trim());

        harness.Run(ActionIds.MergeBranch);
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith(
            "Could not merge topic into main:",
            harness.Statuses.Log.Latest.Text,
            StringComparison.Ordinal);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        using var repository = AheadBranch();
        using var harness = await Harness.LoadAsync(repository);

        Assert.False(harness.Merge.TryExecute(ActionIds.Cancel, null));
    }

    private static TestRepository AheadBranch() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("b.txt", "2"))
            .Checkout("main");

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<TabSession> _sessions = [];
        private BranchSelection _selection = new(null, null, null);

        private Harness()
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Merge = new MergeActions(
                _host,
                TestGit.Services.Merge,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                () => _selection,
                Gate);
        }

        public MergeActions Merge { get; }

        public StatusChannel Statuses { get; } = new();

        public int Refreshes { get; private set; }

        public TabOperationGate Gate { get; } = new();

        public TabId TabId => _host.Workspace.ActiveTabId!.Value;

        private UiPump Pump { get; }

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var harness = new Harness();
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            harness._host.Dispatch(new OpenRepositoryCommand(location));
            var session = new TabSession(
                harness.TabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
            harness._sessions.Add(session);
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                session,
                harness._host,
                new StatusChannel(),
                InlineUi.Post,
                session.Cancellation.Token);

            // What the tab refresher does for the banner: read the stopped operation, if any.
            var operation = new RepositoryOperationDetector().Detect(location);
            var info = await TestGit.Services.OperationInfo.ReadAsync(
                location,
                operation,
                TestContext.Current.CancellationToken);
            harness._host.Dispatch(new TabCommand(harness.TabId, new OperationLoadedCommand(info)));
            return harness;
        }

        public void Select(string? commitHash) => _selection = new BranchSelection(null, null, commitHash);

        public void SelectRow(SidebarRowKind kind, string name, string commitHash) =>
            _selection = new BranchSelection(kind, name, commitHash);

        public void Run(ActionId action) => Assert.True(Merge.TryExecute(action, null), action.Value);

        public Task FinishAsync() => PumpUntilAsync(() => Statuses.Log.Latest is not null && Refreshes > 0
            || Statuses.Log.Latest?.Severity == StatusSeverity.Error);

        public Task PumpUntilAsync(Func<bool> condition) => Pump.RunUntilAsync(condition);

        public void Dispose()
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }
        }
    }
}
