using Gitunia.Core;
using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.App.Operations;

public class OperationControllerTests
{
    private const string FullHash = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void GivenNoOperation_WhenContinuing_ThenItIsRefusedWithoutGit()
    {
        using var harness = new Harness(null);

        harness.Execute(ActionIds.OperationContinue);

        Assert.Equal(OperationTexts.NoOperationNotice, harness.Controller.Notice);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public void GivenConflictedFiles_WhenContinuing_ThenItIsRefusedAndCountsThem()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));
        harness.Send(new WorkingTreeLoadedCommand(WorkingTreeEntries.Snapshot(
            WorkingTreeEntries.Conflict("a.txt"),
            WorkingTreeEntries.Conflict("b.txt"),
            WorkingTreeEntries.Modified("c.txt"))));

        harness.Execute(ActionIds.OperationContinue);

        Assert.StartsWith("2 files still conflicted", harness.Controller.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Writer.Calls);
        Assert.False(harness.Gate.IsAnyBusy);
    }

    [Fact]
    public async Task GivenOneConflictedFile_WhenContinuing_ThenTheRefusalIsSingular()
    {
        using var harness = new Harness(Info(RepositoryOperation.Rebase));
        harness.Send(new WorkingTreeLoadedCommand(WorkingTreeEntries.Snapshot(WorkingTreeEntries.Conflict("a.txt"))));

        harness.Execute(ActionIds.OperationContinue);

        Assert.StartsWith("1 file still conflicted", harness.Controller.Notice, StringComparison.Ordinal);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GivenResolvedMerge_WhenContinuing_ThenTheMessageNamesTheMergeAndTheTabRefreshes()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge, target: "feature/x"));

        harness.Execute(ActionIds.OperationContinue);
        await harness.WaitAsync();

        Assert.Equal(["continue|Merge"], harness.Writer.Calls);
        Assert.Equal("Merged feature/x into main (0123456)", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest.Severity);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenRebaseThatStopsAgain_WhenContinuing_ThenItWarnsAndPointsAtTheConflicts()
    {
        using var harness = new Harness(Info(RepositoryOperation.Rebase));
        harness.Writer.Outcome = OperationOutcome.Conflicted;

        harness.Execute(ActionIds.OperationContinue);
        await harness.WaitAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("stopped on conflicts", latest.Text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenGitRefuses_WhenContinuing_ThenTheErrorShowsGitsReasonAndTheTabRefreshes()
    {
        using var harness = new Harness(Info(RepositoryOperation.CherryPick));
        harness.Writer.Failure = new GitCommandFailedException(["cherry-pick"], 128, "error: no cherry-pick\nmore");

        harness.Execute(ActionIds.OperationContinue);
        await harness.WaitAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Error, latest.Severity);
        Assert.Equal("Could not continue the cherry-pick: error: no cherry-pick", latest.Text);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public void GivenARebase_WhenAborting_ThenTheDialogNamesTheOldTipAndNothingRunsYet()
    {
        using var harness = new Harness(Info(RepositoryOperation.Rebase) with { OriginalHead = FullHash });

        harness.Execute(ActionIds.OperationAbort);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Abort rebase", prompt.Title);
        Assert.Contains("conflict resolutions made so far are lost", prompt.Lines[1], StringComparison.Ordinal);
        Assert.Equal("main goes back to 0123456; that commit stays in the reflog.", prompt.Consequence);
        Assert.Empty(harness.Writer.Calls);
        Assert.False(harness.Gate.IsAnyBusy);
    }

    [Fact]
    public void GivenAMerge_WhenAborting_ThenTheDialogSaysTheBranchHasNotMoved()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));

        harness.Execute(ActionIds.OperationAbort);

        var consequence = Assert.Single(harness.Prompts).Consequence;
        Assert.Equal("The working tree and index go back to 0123456; the branch itself has not moved.", consequence);
    }

    [Fact]
    public async Task GivenTheAbortIsConfirmed_WhenItFinishes_ThenTheMessageSaysWhereTheBranchIs()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));
        harness.Execute(ActionIds.OperationAbort);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["abort|Merge"], harness.Writer.Calls);
        Assert.Equal("Aborted the merge; back to main at 0123456", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public void GivenAMerge_WhenSkipping_ThenItIsRefusedWithoutAsking()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));

        harness.Execute(ActionIds.OperationSkip);

        Assert.Equal(OperationTexts.CannotSkipMerge, harness.Controller.Notice);
        Assert.Empty(harness.Prompts);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public void GivenARebaseStoppedOnACommit_WhenSkipping_ThenTheDialogNamesThatCommit()
    {
        using var harness = new Harness(Info(RepositoryOperation.Rebase, commit: FullHash, subject: "Fix the parser"));

        harness.Execute(ActionIds.OperationSkip);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Skip 0123456 Fix the parser?", prompt.Lines[0]);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public async Task GivenTheSkipIsConfirmed_WhenItFinishes_ThenGitSkippedAndTheMessageNamesTheCommit()
    {
        using var harness = new Harness(Info(RepositoryOperation.Revert, commit: FullHash));
        harness.Execute(ActionIds.OperationSkip);

        harness.Answer();
        await harness.WaitAsync();

        Assert.Equal(["skip|Revert"], harness.Writer.Calls);
        Assert.Equal("Skipped 0123456; the revert is finished (0123456)", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenTheOperationMovedOnWhileAsking_WhenConfirmingTheSkip_ThenNothingIsSkipped()
    {
        using var harness = new Harness(Info(RepositoryOperation.Rebase, commit: FullHash));
        harness.Execute(ActionIds.OperationSkip);
        harness.Send(new OperationLoadedCommand(Info(RepositoryOperation.Rebase, commit: "feedface")));

        harness.Answer();
        await Task.Yield();

        Assert.Empty(harness.Writer.Calls);
        Assert.Contains("nothing was skipped", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.False(harness.Gate.IsAnyBusy);
    }

    [Fact]
    public void GivenAnotherOperationRuns_WhenContinuing_ThenItIsRefusedBusy()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));
        harness.Gate.Begin(harness.TabId);

        harness.Execute(ActionIds.OperationContinue);

        Assert.StartsWith("Busy", harness.Controller.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public void GivenAnotherOperationStartsWhileAsking_WhenConfirmingTheAbort_ThenNothingIsAborted()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));
        harness.Execute(ActionIds.OperationAbort);
        harness.Gate.Begin(harness.TabId);

        harness.Answer();

        Assert.Empty(harness.Writer.Calls);
        Assert.Contains("nothing was aborted", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenTheControllerLeavesItAlone()
    {
        using var harness = new Harness(Info(RepositoryOperation.Merge));

        Assert.False(harness.Controller.TryExecute(ActionIds.ListDown, null));
    }

    private static OperationInfo Info(
        RepositoryOperation kind,
        string? target = null,
        string? commit = null,
        string? subject = null) =>
        new(kind, target, null, null, null, commit, subject, null);

    private sealed class Harness : IDisposable
    {
        private readonly Dispatcher _host = new(AppState.Empty);
        private readonly List<Action> _answers = [];

        public Harness(OperationInfo? info)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-operation-controller-tests");
            var gitDirectory = Path.Combine(root, ".git");
            var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
            _host.Dispatch(new OpenRepositoryCommand(location));
            TabId = _host.Workspace.ActiveTabId!.Value;
            Send(new HeadLoadedCommand(new HeadState(HeadKind.Branch, "main", FullHash)));
            Send(new OperationLoadedCommand(info));
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Controller = new OperationController(
                _host,
                Writer,
                Statuses,
                Pump.Post,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                _ => TestContext.Current.CancellationToken,
                Confirm,
                Gate);
        }

        public TabId TabId { get; }

        public UiPump Pump { get; }

        public FakeOperationWriter Writer { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public OperationController Controller { get; }

        public List<ConfirmPrompt> Prompts { get; } = [];

        public int Refreshes { get; private set; }

        public void Execute(ActionId action) => Controller.TryExecute(action, null);

        public void Send(RepositoryCommand command) => _host.Dispatch(new TabCommand(TabId, command));

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
    }
}
