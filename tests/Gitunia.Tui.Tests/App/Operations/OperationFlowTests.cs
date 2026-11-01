using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.App.Operations;

// Real git on the conflict fixtures of each operation, through the controller, the real writer and the real readers:
// the acceptance of the banner ("works for each operation on conflict fixtures").
public class OperationFlowTests
{
    private const string ConflictFile = "conflict.txt";

    [Theory]
    [InlineData(RepositoryOperation.Merge, "Merged ")]
    [InlineData(RepositoryOperation.Rebase, "Rebased ")]
    [InlineData(RepositoryOperation.CherryPick, "Cherry-pick finished")]
    [InlineData(RepositoryOperation.Revert, "Revert finished")]
    public async Task GivenEachOperationStoppedOnAConflict_WhenResolvedAndContinued_ThenItFinishesAndTheBannerGoes(
        RepositoryOperation operation,
        string expectedStart)
    {
        using var repository = Fixture(operation);
        using var harness = await Harness.CreateAsync(repository);
        Assert.Equal(operation, harness.Tab.Operation!.Kind);
        Assert.EndsWith("1 conflict", BannerDetail(harness), StringComparison.Ordinal);

        harness.Execute(ActionIds.OperationContinue);
        Assert.StartsWith("1 file still conflicted", harness.Controller.Notice, StringComparison.Ordinal);
        Assert.Equal(operation, harness.Detect());

        repository.WriteFile(ConflictFile, "resolved\n");
        repository.Git("add", ConflictFile);
        await harness.ReloadAsync();
        Assert.EndsWith("no conflicts left", BannerDetail(harness), StringComparison.Ordinal);
        harness.Execute(ActionIds.OperationContinue);
        await harness.WaitAsync();

        Assert.Equal(RepositoryOperation.None, harness.Detect());
        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Info, latest.Severity);
        Assert.StartsWith(expectedStart, latest.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge)]
    [InlineData(RepositoryOperation.Rebase)]
    [InlineData(RepositoryOperation.CherryPick)]
    [InlineData(RepositoryOperation.Revert)]
    public async Task GivenEachOperationStoppedOnAConflict_WhenAbortedAfterConfirming_ThenTheOldTipReturns(
        RepositoryOperation operation)
    {
        using var repository = Fixture(operation);
        var before = ReadTip(repository, operation);
        using var harness = await Harness.CreateAsync(repository);

        harness.Execute(ActionIds.OperationAbort);
        Assert.Equal(operation, harness.Detect());
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(RepositoryOperation.None, harness.Detect());
        Assert.Equal(before, repository.Head);
        Assert.StartsWith("Aborted the ", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RepositoryOperation.Rebase)]
    [InlineData(RepositoryOperation.CherryPick)]
    [InlineData(RepositoryOperation.Revert)]
    public async Task GivenEachSkippableOperationOnAConflict_WhenSkippedAfterConfirming_ThenItFinishesWithoutTheCommit(
        RepositoryOperation operation)
    {
        using var repository = Fixture(operation);
        using var harness = await Harness.CreateAsync(repository);
        var commit = harness.Tab.Operation!.Commit;

        harness.Execute(ActionIds.OperationSkip);
        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains(commit![..7], prompt.Lines[0], StringComparison.Ordinal);
        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(RepositoryOperation.None, harness.Detect());
        Assert.StartsWith("Skipped ", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARebaseWithTwoConflicts_WhenContinuing_ThenItStopsAgainAndTheBannerShowsTheNextStep()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile(ConflictFile, "original\n"))
            .CheckoutNewBranch("other")
            .Commit("First", new TestFile(ConflictFile, "first\n"))
            .Commit("Second", new TestFile(ConflictFile, "second\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile(ConflictFile, "main\n"))
            .Checkout("other")
            .RebaseExpectingConflict("main");
        using var harness = await Harness.CreateAsync(repository);
        Assert.StartsWith("step 1/2", BannerDetail(harness), StringComparison.Ordinal);
        repository.WriteFile(ConflictFile, "resolved\n");
        repository.Git("add", ConflictFile);
        await harness.ReloadAsync();

        harness.Execute(ActionIds.OperationContinue);
        await harness.WaitAsync();
        await harness.ReloadAsync();

        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Equal(RepositoryOperation.Rebase, harness.Detect());
        Assert.StartsWith("step 2/2  1 conflict", BannerDetail(harness), StringComparison.Ordinal);
    }

    private static string BannerDetail(Harness harness) =>
        OperationBannerBuilder.Build(
            harness.Tab.Operation,
            OperationBannerBuilder.ConflictCount(harness.Tab.WorkingTree))!.Detail;

    private static string ReadTip(TestRepository repository, RepositoryOperation operation) =>
        operation == RepositoryOperation.Rebase ? repository.Git("rev-parse", "other").Trim() : repository.Head;

    private static TestRepository Fixture(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.Merge => TestRepositories.Conflicted(),
        RepositoryOperation.Rebase => TestRepositories.RebaseConflicted(),
        RepositoryOperation.CherryPick => TestRepositories.CherryPickConflicted(),
        _ => TestRepositories.RevertConflicted(),
    };

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];
        private readonly GitRunner _runner;
        private readonly RepositoryLocation _location;
        private readonly TabId _tabId;

        private Harness(TestRepository repository, RepositoryLocation location)
        {
            _runner = repository.CreateIsolatedRunner();
            _location = location;
            _host.Dispatch(new OpenRepositoryCommand(location));
            _tabId = _host.Workspace.ActiveTabId!.Value;
            Pump = new UiPump(TestContext.Current.CancellationToken);
            var writer = new OperationWriter(new OperationRunner(_runner, new RepositoryOperationDetector()));
            Controller = new OperationController(
                _host,
                writer,
                Statuses,
                Pump.Post,
                _ => true,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                Gate);
        }

        public UiPump Pump { get; }

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public OperationController Controller { get; }

        public List<ConfirmPrompt> Prompts { get; } = [];

        public RepositoryState Tab => _host.Workspace.FindTab(_tabId)!;

        public static async Task<Harness> CreateAsync(TestRepository repository)
        {
            var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
            var token = TestContext.Current.CancellationToken;
            var location = await discovery.DiscoverAsync(repository.WorkingTreePath, token);
            var harness = new Harness(repository, location);
            await harness.ReloadAsync();
            return harness;
        }

        // What the tab refresher does after a change: the head, the status and the operation details.
        public async Task ReloadAsync()
        {
            var token = TestContext.Current.CancellationToken;
            var refs = new RefReader(_runner);
            Send(new HeadLoadedCommand(await refs.ReadHeadAsync(_location, token)));
            var status = await new StatusReader(_runner)
                .ReadStatusAsync(_location, false, UntrackedFilesMode.All, token);
            Send(new WorkingTreeLoadedCommand(status));
            var operation = new RepositoryOperationDetector().Detect(_location);
            Send(new OperationLoadedCommand(await new OperationInfoReader().ReadAsync(_location, operation, token)));
        }

        public RepositoryOperation Detect() => new RepositoryOperationDetector().Detect(_location);

        public void Execute(ActionId action) => Controller.TryExecute(action, null);

        public void Answer()
        {
            foreach (var answer in _answers)
            {
                answer();
            }

            _answers.Clear();
        }

        public Task WaitAsync() => Pump.RunUntilAsync(() => !Gate.IsAnyBusy && Statuses.Log.Latest is not null);

        public void Dispose()
        {
        }

        private bool Confirm(ConfirmPrompt prompt, Action onConfirmed)
        {
            Prompts.Add(prompt);
            _answers.Add(onConfirmed);
            return true;
        }

        private void Send(RepositoryCommand command) => _host.Dispatch(new TabCommand(_tabId, command));
    }
}
