using Gitunia.Tui.Tests.Fakes;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Core.Status;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Worktrees;

public sealed class WorktreeActionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenCleanWorktree_WhenRemovingAndConfirming_ThenDirectoryIsGoneAndBranchKept()
    {
        using var repository = TestRepositories.Linear(2);
        var path = repository.AddWorktree("clean", "clean-branch", createBranch: true);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        var notice = harness.Execute(ActionIds.WorktreeRemove);
        var prompt = Assert.Single(harness.Prompts);
        Assert.True(Directory.Exists(path));
        await harness.AnswerAsync();

        Assert.Equal(string.Empty, notice);
        Assert.Equal("Remove worktree", prompt.Title);
        Assert.Contains(path, prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("clean-branch", prompt.Consequence, StringComparison.Ordinal);
        Assert.False(Directory.Exists(path));
        Assert.Contains("clean-branch", repository.Git("branch", "--list", "clean-branch"));
        Assert.Equal($"Removed worktree {path}", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenConfirmationDropped_WhenRemoving_ThenNothingRuns()
    {
        using var repository = TestRepositories.Linear(2);
        var path = repository.AddWorktree("clean", "clean-branch", createBranch: true);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        harness.Execute(ActionIds.WorktreeRemove);

        Assert.Single(harness.Prompts);
        Assert.True(Directory.Exists(path));
        Assert.Null(harness.Statuses.Log.Latest);
    }

    [Fact]
    public async Task GivenDirtyWorktree_WhenRemoving_ThenSecondConfirmListsChangesAndDecliningKeepsThem()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out var dirtyPath);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, dirtyPath);

        harness.Execute(ActionIds.WorktreeRemove);
        await harness.AnswerAsync();

        Assert.Equal(2, harness.Prompts.Count);
        var second = harness.Prompts[1];
        Assert.Equal("Remove worktree with changes", second.Title);
        Assert.Contains("file.txt", second.Lines[0], StringComparison.Ordinal);
        Assert.Contains("cannot be undone", second.Consequence, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dirtyPath, "file.txt")));
    }

    [Fact]
    public async Task GivenDirtyWorktree_WhenBothConfirmsAreYes_ThenItIsForceRemoved()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out var dirtyPath);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, dirtyPath);

        harness.Execute(ActionIds.WorktreeRemove);
        await harness.AnswerAsync();
        await harness.AnswerAsync();

        Assert.False(Directory.Exists(dirtyPath));
        Assert.Contains("uncommitted changes", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenLockedWorktree_WhenRemoving_ThenRefusedWithoutDialog()
    {
        using var repository = TestRepositories.WithManyWorktrees(out var lockedPath, out _, out _);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, lockedPath);

        var notice = harness.Execute(ActionIds.WorktreeRemove);

        Assert.Contains("locked", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
        Assert.True(Directory.Exists(lockedPath));
    }

    [Fact]
    public async Task GivenPrunableWorktree_WhenRemoving_ThenRefusedPointingToPrune()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out var prunablePath, out _);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, prunablePath);

        var notice = harness.Execute(ActionIds.WorktreeRemove);

        Assert.Contains("prune", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenMainWorktreeRow_WhenRemoving_ThenRefused()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out _);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, repository.WorkingTreePath);

        var notice = harness.Execute(ActionIds.WorktreeRemove);

        Assert.Equal("The main worktree cannot be removed", notice);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenTabOnLinkedWorktree_WhenRemovingItsOwnWorktree_ThenRefused()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var harness = await Harness.LoadAsync(path, path);

        var notice = harness.Execute(ActionIds.WorktreeRemove);

        Assert.Contains("This tab is on that worktree", notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenNoWorktreeRowSelected_WhenRemoving_ThenNoticeAndNoDialog()
    {
        using var repository = TestRepositories.Linear(2);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);

        var notice = harness.Execute(ActionIds.WorktreeRemove);

        Assert.Equal("Select a worktree in the sidebar first", notice);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenWorktree_WhenLockingWithReasonThenUnlocking_ThenListingFollows()
    {
        using var repository = TestRepositories.Linear(2);
        var path = repository.AddWorktree("lockable", "lockable-branch", createBranch: true);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        harness.Execute(ActionIds.WorktreeLock);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("usb stick");
        await harness.AcceptAsync();
        var locked = harness.Tab.Worktrees.Single(worktree => worktree.Path == path);
        harness.Execute(ActionIds.WorktreeUnlock);
        await harness.WaitForRefreshAsync();

        Assert.True(locked.IsLocked);
        Assert.Equal("usb stick", locked.LockReason);
        Assert.False(harness.Tab.Worktrees.Single(worktree => worktree.Path == path).IsLocked);
    }

    [Fact]
    public async Task GivenUnlockedWorktree_WhenUnlocking_ThenNotLockedNotice()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        var notice = harness.Execute(ActionIds.WorktreeUnlock);

        Assert.Contains("is not locked", notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenLockPromptOpen_WhenCancelling_ThenNothingIsLocked()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        harness.Execute(ActionIds.WorktreeLock);
        harness.Execute(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.DoesNotContain("locked", repository.Git("worktree", "list", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenPrunableWorktree_WhenPruningAndConfirming_ThenOnlyItsEntryGoes()
    {
        using var repository =
            TestRepositories.WithManyWorktrees(out var lockedPath, out var prunablePath, out var dirtyPath);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);

        var notice = harness.Execute(ActionIds.WorktreePrune);
        var prompt = Assert.Single(harness.Prompts);
        await harness.AnswerAsync();

        Assert.Equal(string.Empty, notice);
        Assert.Contains(prunablePath, prompt.Lines, StringComparer.Ordinal);
        Assert.DoesNotContain(lockedPath, prompt.Lines, StringComparer.Ordinal);
        var listing = repository.Git("worktree", "list", "--porcelain");
        Assert.DoesNotContain("wt-prunable", listing, StringComparison.Ordinal);
        Assert.Contains("wt-locked", listing, StringComparison.Ordinal);
        Assert.Contains(dirtyPath, harness.Tab.Worktrees.Select(worktree => worktree.Path));
        Assert.Equal("Pruned 1 worktree entry", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenNothingPrunable_WhenPruning_ThenNoDialog()
    {
        using var repository = TestRepositories.WithWorktree(out _);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);

        var notice = harness.Execute(ActionIds.WorktreePrune);

        Assert.Equal("No worktree entry to prune", notice);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenNewBranchName_WhenAddingWithDefaultPath_ThenSiblingWorktreeStartsAtSelectedCommit()
    {
        using var repository = TestRepositories.Linear();
        var start = repository.Git("rev-parse", "HEAD~1").Trim();
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null, start);
        var expected = WorktreeTexts.DefaultPath(repository.WorkingTreePath, "feature/x");

        harness.Execute(ActionIds.WorktreeAdd);
        harness.Type("feature/x");
        harness.Execute(ActionIds.InputAccept);
        Assert.Contains(expected, PopupText.Of(harness.Actions), StringComparison.Ordinal);
        await harness.AcceptAsync();

        Assert.Equal(start, repository.GitIn(expected, ["rev-parse", "HEAD"]).Trim());
        Assert.Equal("feature/x", repository.GitIn(expected, ["branch", "--show-current"]).Trim());
        Assert.Contains(harness.Tab.Worktrees, worktree => worktree.BranchFullName == "refs/heads/feature/x");
    }

    [Fact]
    public async Task GivenBranchHeldByAnotherWorktree_WhenAdding_ThenRefusedBeforeThePathPrompt()
    {
        using var repository = TestRepositories.WithWorktree(out _);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);

        harness.Execute(ActionIds.WorktreeAdd);
        harness.Type("wt-branch");
        harness.Execute(ActionIds.InputAccept);

        Assert.Contains("already checked out", harness.Actions.Notice, StringComparison.Ordinal);
        Assert.Equal(string.Empty, PopupText.Of(harness.Actions));
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenExistingFreeBranch_WhenAdding_ThenWorktreeChecksItOut()
    {
        using var repository = TestRepositories.Linear(2).Branch("free");
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);
        var expected = WorktreeTexts.DefaultPath(repository.WorkingTreePath, "free");

        harness.Execute(ActionIds.WorktreeAdd);
        harness.Type("free");
        harness.Execute(ActionIds.InputAccept);
        await harness.AcceptAsync();

        Assert.Equal("free", repository.GitIn(expected, ["branch", "--show-current"]).Trim());
    }

    [Fact]
    public async Task GivenPathThatExistsAndIsNotEmpty_WhenAdding_ThenGitsMessageIsShownAndNothingIsCreated()
    {
        using var repository = TestRepositories.Linear(2);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null);
        var occupied = WorktreeTexts.DefaultPath(repository.WorkingTreePath, "taken");
        Directory.CreateDirectory(occupied);
        File.WriteAllText(Path.Combine(occupied, "keep.txt"), "x");

        harness.Execute(ActionIds.WorktreeAdd);
        harness.Type("taken");
        harness.Execute(ActionIds.InputAccept);
        await harness.AcceptAsync();

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not add the worktree", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(occupied, "keep.txt")));
    }

    [Fact]
    public async Task GivenStashRowSelected_WhenAddingNewBranch_ThenStartPointIgnoresTheStashCommit()
    {
        using var repository = TestRepositories.Linear(2);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null, null, StashRow());
        var expected = WorktreeTexts.DefaultPath(repository.WorkingTreePath, "from-head");

        harness.Execute(ActionIds.WorktreeAdd);
        harness.Type("from-head");
        harness.Execute(ActionIds.InputAccept);
        await harness.AcceptAsync();

        Assert.Equal(repository.Head, repository.GitIn(expected, ["rev-parse", "HEAD"]).Trim());
    }

    [Fact]
    public async Task GivenFreeBranchRow_WhenOpening_ThenOnlyThePathIsAskedAndTheNewWorktreeIsOpened()
    {
        using var repository = TestRepositories.Linear(2).Branch("free");
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, null, null, BranchRow("free"));
        var expected = WorktreeTexts.DefaultPath(repository.WorkingTreePath, "free");

        harness.Execute(ActionIds.WorktreeOpen);
        Assert.Contains(expected, PopupText.Of(harness.Actions), StringComparison.Ordinal);
        Assert.Empty(harness.Opened);
        await harness.AcceptAsync();

        Assert.Equal("free", repository.GitIn(expected, ["branch", "--show-current"]).Trim());
        Assert.Equal(expected, Assert.Single(harness.Opened));
    }

    [Fact]
    public async Task GivenWorktreeRow_WhenOpening_ThenThisControllerLeavesItToTheSwitchActions()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var harness = await Harness.LoadAsync(repository.WorkingTreePath, path);

        Assert.False(harness.Actions.TryExecute(ActionIds.WorktreeOpen, null));
    }

    private static SidebarRow BranchRow(string name) => new(
        SidebarRowKind.LocalBranch,
        SidebarSection.Branches,
        name,
        string.Empty,
        null,
        false,
        false,
        0,
        null,
        null);

    private static SidebarRow StashRow() => new(
        SidebarRowKind.Stash,
        SidebarSection.Stashes,
        "stash@{0}",
        string.Empty,
        "1111111111111111111111111111111111111111",
        false,
        false,
        0,
        null,
        null);

    [Fact]
    public void GivenBranchWithSlash_WhenDefaultingPath_ThenSiblingDirectoryHasNoSeparator()
    {
        var path = WorktreeTexts.DefaultPath(Path.Combine(Path.GetTempPath(), "parent", "repo"), "fix/12-a");

        Assert.Equal(Path.Combine(Path.GetTempPath(), "parent", "fix-12-a"), path);
    }

    private sealed class Harness : IDisposable
    {
        private readonly RepositoryLocation _location;
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _callbacks = [];
        private readonly SemaphoreSlim _refreshed = new(0);

        private Harness(RepositoryLocation location, SidebarRow? row, string? selectedCommit)
        {
            _location = location;
            Row = row;
            Actions = new WorktreeActions(
                _host,
                TestGit.Services.Worktrees,
                Statuses,
                InlineUi.Post,
                _ => CancellationToken.None,
                Refresh,
                Confirm,
                new TabOperationGate(),
                () => Row,
                () => selectedCommit,
                mode => Mode = mode,
                () => { })
            {
                OpenWorktree = (_, path) => Opened.Add(path),
            };
        }

        public WorktreeActions Actions { get; }

        public StatusChannel Statuses { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public List<string> Opened { get; } = [];

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public SidebarRow? Row { get; }

        public RepositoryState Tab => _host.Workspace.ActiveTab!;

        public static async Task<Harness> LoadAsync(
            string tabPath,
            string? rowPath,
            string? selectedCommit = null,
            SidebarRow? selectedRow = null)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(tabPath, Token);
            var row = rowPath is null
                ? null
                : new SidebarRow(
                    SidebarRowKind.Worktree,
                    SidebarSection.Worktrees,
                    Path.GetFileName(rowPath),
                    string.Empty,
                    null,
                    false,
                    false,
                    0,
                    null,
                    null,
                    rowPath);
            var harness = new Harness(location, selectedRow ?? row, selectedCommit);
            harness._host.Dispatch(new OpenRepositoryCommand(location));
            await harness.ReloadAsync();
            return harness;
        }

        public string Execute(ActionId action)
        {
            Actions.TryExecute(action, null);
            return Actions.Notice;
        }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Actions.HandleTextKey(new KeyEvent(character.ToString()));
            }
        }

        /// <summary>Accepts the open prompt and waits until the git call and the refresh behind it are done.</summary>
        public async Task AcceptAsync()
        {
            Actions.TryExecute(ActionIds.InputAccept, null);
            await WaitForRefreshAsync();
        }

        /// <summary>Answers the latest dialog with <c>y</c> and waits for the refresh that follows, or for a further
        /// dialog.</summary>
        public async Task AnswerAsync()
        {
            var shown = Prompts.Count;
            var callback = _callbacks[^1];
            _callbacks.Clear();
            callback();
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (_refreshed.CurrentCount == 0 && Prompts.Count == shown && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1, Token);
            }

            if (_refreshed.CurrentCount > 0)
            {
                await WaitForRefreshAsync();
            }

            Assert.True(Prompts.Count > shown || Statuses.Log.Latest is not null);
        }

        public async Task WaitForRefreshAsync()
        {
            Assert.True(await _refreshed.WaitAsync(TimeSpan.FromSeconds(30), Token));
            await ReloadAsync();
        }

        public void Dispose() => _refreshed.Dispose();

        // What the tab's refresh does in the app: read the repository again.
        private async Task ReloadAsync()
        {
            var tabId = _host.Workspace.ActiveTabId!.Value;
            var details = new CommitDetailsStore(
                TestGit.Services.CommitDetails,
                _location,
                action => action(),
                TimeSpan.Zero);
            var session = new TabSession(tabId, GraphGlyphSet.Unicode, details);
            await TestGit.Services.RepositoryLoader
                .RunAsync(_location, session, _host, new StatusChannel(), InlineUi.Post, Token);
        }

        private bool Refresh(TabId tabId)
        {
            _refreshed.Release();
            return true;
        }

        private bool Confirm(ConfirmPrompt prompt, Action onConfirmed)
        {
            Prompts.Add(prompt);
            _callbacks.Add(onConfirmed);
            return true;
        }
    }
}
