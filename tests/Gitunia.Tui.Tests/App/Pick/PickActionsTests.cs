using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Pick;

public class PickActionsTests
{
    [Fact]
    public async Task GivenAGraphCommit_WhenCherryPicking_ThenItLandsOnHeadAndTheStatusNamesTheUndo()
    {
        using var repository = Diverged();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "topic"));

        harness.Run(ActionIds.PickCherry);
        await harness.FinishAsync();

        Assert.Equal("topic two", repository.Git("log", "-1", "--format=%s").Trim());
        var text = harness.Statuses.Log.Latest!.Text;
        Assert.StartsWith("Cherry-picked ", text, StringComparison.Ordinal);
        Assert.Contains("onto main", text, StringComparison.Ordinal);
        Assert.Contains($"git reset --hard {old[..7]}", text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
        Assert.Equal(1, harness.RangeEnds);
    }

    [Fact]
    public async Task GivenARangeNewestFirst_WhenCherryPicking_ThenTheCommitsAreAppliedOldestFirst()
    {
        using var repository = Diverged().Checkout("topic")
            .Commit("topic three", new TestFile("c.txt", "3"))
            .Checkout("main");
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "topic"), Hash(repository, "topic~1"));

        harness.Run(ActionIds.PickCherry);
        await harness.FinishAsync();

        Assert.Equal("topic three\ntopic two", repository.Git("log", "-2", "--format=%s").Trim().Replace("\r", ""));
        Assert.Contains("2 commits", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARangeNewestFirst_WhenReverting_ThenBothAreUndone()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("b.txt", "2"))
            .Commit("three", new TestFile("c.txt", "3"));
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "HEAD"), Hash(repository, "HEAD~1"));

        harness.Run(ActionIds.PickRevert);
        await harness.FinishAsync();

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "c.txt")));
        Assert.StartsWith("Reverted 2 commits onto main", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABranchRow_WhenCherryPicking_ThenTheTipOfTheBranchIsPicked()
    {
        using var repository = Diverged();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(SidebarRowKind.LocalBranch, Hash(repository, "topic"));

        harness.Run(ActionIds.PickCherry);
        await harness.FinishAsync();

        Assert.Equal("topic two", repository.Git("log", "-1", "--format=%s").Trim());
    }

    [Fact]
    public async Task GivenACommitHeadAlreadyHas_WhenCherryPicking_ThenTheStatusSaysNothingToApply()
    {
        using var repository = Diverged();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "main~1"));

        harness.Run(ActionIds.PickCherry);
        await harness.FinishAsync();

        Assert.StartsWith("Nothing to cherry-pick", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAConflictingCommit_WhenCherryPicking_ThenAWarningPointsToContinueAbortAndSkip()
    {
        using var repository = TestRepositories.ConflictingBranches();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "other"));

        harness.Run(ActionIds.PickCherry);
        await harness.FinishAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("<space>gc", latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>ga", latest.Text, StringComparison.Ordinal);
        Assert.Contains("<space>gs", latest.Text, StringComparison.Ordinal);
        Assert.Contains("UU conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.CherryPick, new RepositoryOperationDetector().Detect(harness.Location));
    }

    [Fact]
    public async Task GivenAConflictingRevert_WhenReverting_ThenAWarningNamesTheRevert()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .Commit("First change", new TestFile("conflict.txt", "first\n"))
            .Commit("Second change", new TestFile("conflict.txt", "second\n"));
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "HEAD~1"));

        harness.Run(ActionIds.PickRevert);
        await harness.FinishAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.StartsWith("The revert of ", latest.Text, StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.Revert, new RepositoryOperationDetector().Detect(harness.Location));
    }

    [Theory]
    [InlineData("conflicted")]
    [InlineData("rebase")]
    [InlineData("cherry-pick")]
    [InlineData("revert")]
    public async Task GivenAnOperationInProgress_WhenPicking_ThenItIsRefusedByNameAndNothingRuns(string kind)
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
        harness.SelectCommits(head);

        harness.Run(ActionIds.PickCherry);
        harness.Run(ActionIds.PickRevert);

        var name = kind == "conflicted" ? "merge" : kind;
        Assert.Contains($"finish or abort the {name} first", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.Contains("<space>gc", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
        Assert.Equal(0, harness.Refreshes);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenCherryPicking_ThenThePromptListsTheParentsAndOneKeyRuns()
    {
        using var repository = MergedTopic();
        var merge = repository.Head;
        repository.Checkout("base");
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(merge);

        harness.Run(ActionIds.PickCherry);
        await harness.PumpUntilAsync(() => harness.Pick.StatusLine.Length > 0);

        var line = harness.Pick.StatusLine;
        Assert.Contains("[1]", line, StringComparison.Ordinal);
        Assert.Contains("main two", line, StringComparison.Ordinal);
        Assert.Contains("[2]", line, StringComparison.Ordinal);
        Assert.Contains("topic two", line, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.False(harness.Gate.IsBusy(harness.TabId));

        harness.Press("1");
        await harness.FinishAsync();

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.StartsWith("Cherry-picked ", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheMainlinePrompt_WhenAParentRowIsClicked_ThenItRunsWithThatParentLikeTheKey()
    {
        using var repository = MergedTopic();
        var merge = repository.Head;
        repository.Checkout("base");
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(merge);
        harness.Run(ActionIds.PickCherry);
        await harness.PumpUntilAsync(() => harness.Pick.Prompt is not null);

        Assert.Equal(2, harness.Pick.Prompt!.Parents.Count);
        Assert.Contains("topic two", harness.Pick.Prompt.Parents[1], StringComparison.Ordinal);
        Assert.True(harness.Pick.TryExecute(ActionIds.PickMainline, 1));
        await harness.FinishAsync();

        Assert.Null(harness.Pick.Prompt);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.StartsWith("Cherry-picked ", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheMainlinePrompt_WhenAnOutOfRangeRowIsClicked_ThenItStaysOpenAndNothingRuns()
    {
        using var repository = MergedTopic();
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(head);
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.Prompt is not null);

        Assert.True(harness.Pick.TryExecute(ActionIds.PickMainline, 3));

        Assert.NotNull(harness.Pick.Prompt);
        Assert.Contains("Choose a parent from 1 to 2", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenTheMainlinePrompt_WhenTheBackdropCancels_ThenThePromptCloses()
    {
        using var repository = MergedTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(repository.Head);
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.Prompt is not null);

        Assert.True(harness.Pick.TryExecute(ActionIds.Cancel, null));

        Assert.Null(harness.Pick.Prompt);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenTheMainlinePrompt_WhenEscapeIsPressed_ThenNothingRunsAndTheModeReturns()
    {
        using var repository = MergedTopic();
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(head);

        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.StatusLine.Length > 0);
        Assert.True(harness.Pick.TryExecute(ActionIds.Cancel, null));

        Assert.Equal(string.Empty, harness.Pick.StatusLine);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(head, repository.Head);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenTheMainlinePrompt_WhenAParentOutOfRangeIsPressed_ThenItStaysOpen()
    {
        using var repository = MergedTopic();
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(head);
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.StatusLine.Length > 0);

        harness.Press("5");

        Assert.Contains("Choose a parent from 1 to 2", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, harness.Pick.StatusLine);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAPlainCommitInTheRange_WhenThePromptOpens_ThenOnlyParentOneIsUsableAndTwoIsRefused()
    {
        using var repository = MergedTopic().Commit("after", new TestFile("d.txt", "4"));
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "HEAD"), Hash(repository, "HEAD~1"));
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.Prompt is not null);

        Assert.Equal(1, harness.Pick.Prompt!.UsableParents);
        Assert.True(harness.Pick.TryExecute(ActionIds.PickMainline, 2));

        Assert.NotNull(harness.Pick.Prompt);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenOnlyMergeCommits_WhenThePromptOpens_ThenEveryParentIsUsable()
    {
        using var repository = MergedTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(repository.Head);
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.Prompt is not null);

        Assert.Equal(2, harness.Pick.Prompt!.UsableParents);
    }

    [Fact]
    public async Task GivenAMergeCommitInARange_WhenReverting_ThenTheMainlineIsAskedOnceForTheWholeRange()
    {
        using var repository = MergedTopic().Commit("after", new TestFile("d.txt", "4"));
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "HEAD"), Hash(repository, "HEAD~1"));
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.StatusLine.Length > 0);

        harness.Press("1");
        await harness.FinishAsync();

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "d.txt")));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.StartsWith("Reverted 2 commits", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMergeAndAPlainCommit_WhenParentTwoIsPressed_ThenItIsRefusedAndNothingRuns()
    {
        using var repository = MergedTopic().Commit("after", new TestFile("d.txt", "4"));
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "HEAD"), Hash(repository, "HEAD~1"));
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.StatusLine.Length > 0);

        harness.Press("2");

        Assert.Contains("every commit is a merge", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, harness.Pick.StatusLine);
        Assert.Equal(head, repository.Head);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenParentTwoIsPressed_ThenTheOtherSideIsReverted()
    {
        using var repository = MergedTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(repository.Head);
        harness.Run(ActionIds.PickRevert);
        await harness.PumpUntilAsync(() => harness.Pick.StatusLine.Length > 0);

        harness.Press("2");
        await harness.FinishAsync();

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "m.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenADetachedHead_WhenCherryPicking_ThenItIsAllowedAndNamesTheDetachedHead()
    {
        using var repository = Diverged();
        repository.Checkout(repository.Head);
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "topic"));

        harness.Run(ActionIds.PickCherry);
        await harness.FinishAsync();

        Assert.Contains("onto the detached HEAD", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal("topic two", repository.Git("log", "-1", "--format=%s").Trim());
    }

    [Fact]
    public async Task GivenAnUnbornBranch_WhenPicking_ThenItIsRefused()
    {
        using var repository = TestRepository.Create();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(new string('a', 40));

        harness.Run(ActionIds.PickCherry);

        Assert.Contains("no commits yet", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public async Task GivenASidebarRowThatIsNotARef_WhenPicking_ThenNothingRuns(SidebarRowKind kind)
    {
        using var repository = Diverged();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectRow(kind, Hash(repository, "topic"));

        harness.Run(ActionIds.PickCherry);

        Assert.Contains("Select a commit", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
        Assert.Equal(0, harness.RangeEnds);
    }

    [Fact]
    public async Task GivenNothingSelected_WhenPicking_ThenItAsksForASelection()
    {
        using var repository = Diverged();
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits();

        harness.Run(ActionIds.PickRevert);

        Assert.Contains("Select a commit", harness.Pick.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnotherOperationOfTheTab_WhenPicking_ThenItIsBusyAndTheGateIsKept()
    {
        using var repository = Diverged();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "topic"));
        harness.Gate.Begin(harness.TabId);

        harness.Run(ActionIds.PickCherry);

        Assert.Contains("Busy", harness.Pick.Notice, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
        Assert.True(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenADirtyTreeGitCannotPickOver_WhenCherryPicking_ThenGitsMessageIsShownAsAnError()
    {
        using var repository = Diverged().WriteFile("b.txt", "dirty");
        using var harness = await Harness.LoadAsync(repository);
        harness.SelectCommits(Hash(repository, "topic"));

        harness.Run(ActionIds.PickCherry);
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not cherry-pick ", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.False(harness.Gate.IsBusy(harness.TabId));
    }

    [Fact]
    public async Task GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotHandled()
    {
        using var repository = Diverged();
        using var harness = await Harness.LoadAsync(repository);

        Assert.False(harness.Pick.TryExecute(ActionIds.Cancel, null));
    }

    // Main has "main two" (m.txt), topic has "topic two" (b.txt), both forked from "one".
    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"));

    // Branch base at "one"; main gets m.txt and merges topic (b.txt), so HEAD is a merge commit.
    private static TestRepository MergedTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Branch("base")
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"))
            .Merge("topic");

    private static string Hash(TestRepository repository, string revision) =>
        repository.Git("rev-parse", revision).Trim();

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<TabSession> _sessions = [];
        private PickSelection _selection = new(null, []);

        private Harness(GitServices services)
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Pick = new PickActions(
                _host,
                services.Pick,
                services.PickCommits,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                mode => Mode = mode,
                () => { },
                () => RangeEnds++,
                () => _selection,
                Gate);
        }

        public PickActions Pick { get; }

        public StatusChannel Statuses { get; } = new();

        public int Refreshes { get; private set; }

        public int RangeEnds { get; private set; }

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public TabOperationGate Gate { get; } = new();

        public RepositoryLocation Location { get; private set; } = null!;

        public TabId TabId => _host.Workspace.ActiveTabId!.Value;

        private UiPump Pump { get; }

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var services = GitServices.Create(repository.CreateIsolatedRunner(), static () => BranchColors.SlotCount);
            var harness = new Harness(services);
            var location = await services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            harness.Location = location;
            harness._host.Dispatch(new OpenRepositoryCommand(location));
            var session = new TabSession(
                harness.TabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(services.CommitDetails, location, action => action(), TimeSpan.Zero));
            harness._sessions.Add(session);
            await services.RepositoryLoader.RunAsync(
                location,
                session,
                harness._host,
                new StatusChannel(),
                InlineUi.Post,
                session.Cancellation.Token);

            // What the tab refresher does for the banner: read the stopped operation, if any.
            var operation = new RepositoryOperationDetector().Detect(location);
            var info = await services.OperationInfo.ReadAsync(
                location,
                operation,
                TestContext.Current.CancellationToken);
            harness._host.Dispatch(new TabCommand(harness.TabId, new OperationLoadedCommand(info)));
            return harness;
        }

        public void SelectCommits(params string[] hashes) => _selection = new PickSelection(null, hashes);

        public void SelectRow(SidebarRowKind kind, string commitHash) =>
            _selection = new PickSelection(kind, [commitHash]);

        public void Run(ActionId action) => Assert.True(Pick.TryExecute(action, null), action.Value);

        public void Press(string key) => Assert.True(Pick.HandleTextKey(new KeyEvent(key)));

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
