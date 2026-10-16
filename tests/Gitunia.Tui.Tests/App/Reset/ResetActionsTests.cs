using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Reset;

public class ResetActionsTests
{
    [Fact]
    public async Task GivenACommit_WhenChoosingSoft_ThenItRunsAtOnceKeepsTheChangeStagedAndNamesTheOldTip()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(first);

        harness.Run(ActionIds.ResetBranch);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Contains("[s]oft", harness.Reset.StatusLine, StringComparison.Ordinal);
        harness.Press("s");
        await harness.FinishAsync();

        Assert.Empty(harness.Prompts);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(first, repository.Head);
        Assert.Equal("M  a.txt", repository.Git("status", "--porcelain").TrimEnd());
        var text = harness.Statuses.Log.Latest!.Text;
        Assert.Contains($"was {old[..7]}", text, StringComparison.Ordinal);
        Assert.Contains($"git reset --soft {old}", text, StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenNothingStaged_WhenChoosingMixed_ThenItRunsAtOnceAndKeepsTheFiles()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "unstaged edit");
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(first);

        harness.Run(ActionIds.ResetBranch);
        harness.Press("m");
        await harness.FinishAsync();

        Assert.Empty(harness.Prompts);
        Assert.Equal(first, repository.Head);
        Assert.Equal(" M a.txt", repository.Git("status", "--porcelain").TrimEnd());
        Assert.Equal("unstaged edit", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenStagedChanges_WhenChoosingMixed_ThenAConfirmNamesThemAndNothingMovesUntilYes()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "staged edit");
        repository.Git("add", "a.txt");
        repository.WriteFile("a.txt", "staged edit and more");
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Press("m");
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Equal("Mixed reset", prompt.Title);
        Assert.Contains("1 file loses the staged state (1 partially staged)", string.Join(' ', prompt.Lines), StringComparison.Ordinal);
        Assert.Contains("cannot be recovered from the reflog", prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains($"git reset --mixed {old}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);

        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(FirstCommit(repository), repository.Head);
        Assert.Equal(" M a.txt", repository.Git("status", "--porcelain").TrimEnd());
    }

    [Fact]
    public async Task GivenADirtyTree_WhenChoosingHard_ThenTheConfirmListsTheFilesAndTheUndoAndYesDropsThem()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "dirty");
        repository.WriteFile("untracked.txt", "keep");
        var old = repository.Head;
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(first);

        harness.Run(ActionIds.ResetBranch);
        harness.Press("h");
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);

        var prompt = Assert.Single(harness.Prompts);
        var lines = string.Join('\n', prompt.Lines);
        Assert.Equal("Hard reset", prompt.Title);
        Assert.Contains($"to {first[..7]} with --hard", lines, StringComparison.Ordinal);
        Assert.Contains("1 file with uncommitted changes will be overwritten", lines, StringComparison.Ordinal);
        Assert.Contains("  a.txt", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("untracked.txt", lines, StringComparison.Ordinal);
        Assert.Contains("Untracked files are not touched", lines, StringComparison.Ordinal);
        Assert.Contains($"git reset --hard {old}", prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("ORIG_HEAD", prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("cannot be restored", prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
        Assert.Equal("dirty", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));

        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(first, repository.Head);
        Assert.Equal("1", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "untracked.txt")));
    }

    [Fact]
    public async Task GivenManyChangedFiles_WhenChoosingHard_ThenTheConfirmCountsAllAndListsOnlyTheFirstFew()
    {
        using var repository = TestRepository.Create()
            .Commit("one", [.. Enumerable.Range(0, 8).Select(index => new TestFile($"f{index}.txt", "1"))])
            .Commit("two", new TestFile("f0.txt", "2"));
        foreach (var index in Enumerable.Range(0, 8))
        {
            repository.WriteFile($"f{index}.txt", "dirty");
        }

        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Press("h");
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);

        var lines = string.Join('\n', harness.Prompts[0].Lines);
        Assert.Contains("8 files with uncommitted changes", lines, StringComparison.Ordinal);
        Assert.Contains("and 2 more", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("f7.txt", lines, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACleanTree_WhenChoosingHard_ThenTheConfirmSaysThereIsNothingToLose()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Press("h");
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);

        Assert.Contains(
            "no uncommitted changes to lose",
            string.Join(' ', harness.Prompts[0].Lines),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheModePrompt_WhenCancelling_ThenNothingRunsAndNormalModeReturns()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Run(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Reset.StatusLine);
        Assert.Equal(old, repository.Head);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenTheModePrompt_WhenTypingAnotherKey_ThenThePromptStaysOpen()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Press("x");
        harness.Press("S");

        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Contains("[h]ard", harness.Reset.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAConfirmThatWasDeclined_WhenHard_ThenNothingMoves()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Press("h");
        await harness.PumpUntilAsync(() => harness.Prompts.Count > 0);
        harness.Decline();

        Assert.Equal(old, repository.Head);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenAScreenTooSmallForTheDialog_WhenHard_ThenItWarnsAndNothingMoves()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.CanShowDialog = false;
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);
        harness.Press("h");
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("nothing was reset", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenADetachedHead_WhenResetting_ThenItIsRefusedWithoutAPrompt()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        repository.Checkout(old);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(FirstCommit(repository));

        harness.Run(ActionIds.ResetBranch);

        Assert.Contains("HEAD is detached", harness.Reset.Notice, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenResetting_ThenItIsRefusedAndTheMergeStays()
    {
        using var repository = TestRepositories.Conflicted();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(repository.Git("rev-list", "--max-parents=0", "HEAD").Trim());

        harness.Run(ActionIds.ResetBranch);

        Assert.Equal("A merge is in progress: finish or abort the merge first", harness.Reset.Notice);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(old, repository.Head);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, ".git", "MERGE_HEAD")));
    }

    [Fact]
    public async Task GivenNothingSelected_WhenResetting_ThenNoPromptOpens()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(null);

        harness.Run(ActionIds.ResetBranch);

        Assert.Contains("Select a commit", harness.Reset.Notice, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenACommitPassedByAnotherView_WhenBegun_ThenTheSameFlowRunsOnIt()
    {
        using var repository = TwoCommits();
        var first = FirstCommit(repository);
        using var harness = await Harness.LoadAsync(repository);

        var notice = harness.Reset.Begin(harness.TabId, first);
        harness.Press("s");
        await harness.FinishAsync();

        Assert.Equal(string.Empty, notice);
        Assert.Equal(first, repository.Head);
    }

    [Fact]
    public async Task GivenAFailingReset_WhenRunning_ThenTheErrorIsReportedAndNothingIsClaimed()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        using var harness = await Harness.LoadAsync(repository);

        harness.Reset.Begin(harness.TabId, new string('a', 40));
        harness.Press("s");
        await harness.PumpUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not reset:", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
    }

    private static TestRepository TwoCommits() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));

    private static string FirstCommit(TestRepository repository) =>
        repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];
        private readonly List<TabSession> _sessions = [];
        private string? _selectedHash;

        private Harness()
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Reset = new ResetActions(
                _host,
                TestGit.Services.Reset,
                TestGit.Services.Status,
                new RepositoryOperationDetector(),
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                Confirm,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                mode => Mode = mode,
                () => { },
                () => new BranchSelection(null, null, _selectedHash));
        }

        public ResetActions Reset { get; }

        public StatusChannel Statuses { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public int Refreshes { get; private set; }

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public bool CanShowDialog { get; set; } = true;

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
            return harness;
        }

        public void Select(string? commitHash) => _selectedHash = commitHash;

        public void Run(ActionId action) => Assert.True(Reset.TryExecute(action, null), action.Value);

        public void Press(string key) => Assert.True(Reset.HandleTextKey(new KeyEvent(key)));

        /// <summary>Presses <c>y</c> in the open dialog.</summary>
        public void Answer()
        {
            var answers = _answers.ToList();
            _answers.Clear();
            foreach (var answer in answers)
            {
                answer();
            }
        }

        /// <summary>Presses <c>n</c> in the open dialog: the callback is dropped.</summary>
        public void Decline() => _answers.Clear();

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

        private bool Confirm(ConfirmPrompt prompt, Action onYes)
        {
            if (!CanShowDialog)
            {
                return false;
            }

            Prompts.Add(prompt);
            _answers.Add(onYes);
            return true;
        }
    }
}
