using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Branches;

public class BranchActionsTests
{
    private static TestRepository RepositoryWithTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"))
            .Branch("topic");

    public static TheoryData<string> OperationRepositories =>
    [
        nameof(TestRepositories.Conflicted),
        nameof(TestRepositories.RebaseConflicted),
        nameof(TestRepositories.CherryPickConflicted),
        nameof(TestRepositories.RevertConflicted),
    ];

    [Theory]
    [MemberData(nameof(OperationRepositories))]
    public async Task GivenAnOperationInProgress_WhenSwitchingFromTheSidebar_ThenItIsRefusedAndNoGitRuns(string kind)
    {
        using var repository = OperationRepository(kind);
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        await harness.LoadOperationAsync();
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "other", head));

        harness.Run(ActionIds.BranchSwitch);

        AssertRefused(harness, kind);
        Assert.Equal(head, repository.Head);
    }

    [Theory]
    [MemberData(nameof(OperationRepositories))]
    public async Task GivenAnOperationInProgress_WhenCreatingWithCheckoutFromTheGraph_ThenItIsRefusedAndNoPromptOpens(
        string kind)
    {
        using var repository = OperationRepository(kind);
        using var harness = await Harness.LoadAsync(repository);
        await harness.LoadOperationAsync();
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.BranchCreate);

        AssertRefused(harness, kind);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Theory]
    [MemberData(nameof(OperationRepositories))]
    public async Task GivenAnOperationInProgress_WhenCheckingOutADetachedCommit_ThenItIsRefusedAndNoGitRuns(string kind)
    {
        using var repository = OperationRepository(kind);
        var head = repository.Head;
        using var harness = await Harness.LoadAsync(repository);
        await harness.LoadOperationAsync();

        var refusal = harness.Branches.CheckoutDetached(harness.TabId, head);
        var creation = harness.Branches.BeginCreate(harness.TabId, head, checkout: true);

        Assert.Contains("finish or abort the", refusal, StringComparison.Ordinal);
        Assert.Contains("<space>gc", refusal, StringComparison.Ordinal);
        Assert.Equal(refusal, creation);
        Assert.Equal(0, harness.Refreshes);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenCreatingWithoutCheckout_ThenThePromptStillOpens()
    {
        using var repository = OperationRepository(nameof(TestRepositories.Conflicted));
        using var harness = await Harness.LoadAsync(repository);
        await harness.LoadOperationAsync();
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.BranchCreateOnly);

        Assert.Equal(string.Empty, harness.Branches.Notice);
        Assert.Equal(KeyMode.Insert, harness.Mode);
    }

    private static TestRepository OperationRepository(string kind) => kind switch
    {
        nameof(TestRepositories.Conflicted) => TestRepositories.Conflicted(),
        nameof(TestRepositories.RebaseConflicted) => TestRepositories.RebaseConflicted(),
        nameof(TestRepositories.CherryPickConflicted) => TestRepositories.CherryPickConflicted(),
        _ => TestRepositories.RevertConflicted(),
    };

    private static void AssertRefused(Harness harness, string kind)
    {
        var name = kind switch
        {
            nameof(TestRepositories.Conflicted) => "merge",
            nameof(TestRepositories.RebaseConflicted) => "rebase",
            nameof(TestRepositories.CherryPickConflicted) => "cherry-pick",
            _ => "revert",
        };
        Assert.Contains($"finish or abort the {name} first", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Contains("<space>gc", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Contains("<space>ga", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenABranchRow_WhenCreatingWithCheckout_ThenTheBranchStartsThereAndIsCheckedOut()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchCreate);
        harness.Type("feature/new");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("feature/new", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(repository.Head, repository.Git("rev-parse", "topic").Trim());
        Assert.Equal(1, harness.Refreshes);
        Assert.Contains("Created branch feature/new", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAGraphCommit_WhenCreatingWithoutCheckout_ThenHeadStaysAndTheBranchIsAtTheCommit()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, first));

        harness.Run(ActionIds.BranchCreateOnly);
        Assert.Contains("(not checked out)", harness.Branches.StatusLine, StringComparison.Ordinal);
        harness.Type("old");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(first, repository.Git("rev-parse", "old").Trim());
    }

    [Fact]
    public async Task GivenNothingSelected_WhenCreating_ThenNoPromptOpensAndNothingRuns()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.BranchCreate);

        Assert.Equal("Select a commit or a branch first", harness.Branches.Notice);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenANameInUse_WhenAccepting_ThenGitIsNotRunAndTheNoticeSaysSo()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.BranchCreate);
        harness.Type("topic");
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("Branch topic already exists", harness.Branches.Notice);
        Assert.Equal(0, harness.Refreshes);
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenAccepting_ThenNothingIsCreatedAndTheErrorIsShown()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.BranchCreate);
        harness.Type("bad name");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("not a valid branch name", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "bad name").Trim());
    }

    [Fact]
    public async Task GivenAnOpenPrompt_WhenEscapeIsPressed_ThenItClosesAndGitIsNotRun()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));
        harness.Run(ActionIds.BranchCreate);
        harness.Type("nope");

        harness.Run(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "nope").Trim());
        Assert.False(harness.Branches.TryExecute(ActionIds.Cancel, null));
    }

    [Fact]
    public async Task GivenAFilterText_WhenSwitching_ThenTheBestMatchIsCheckedOut()
    {
        using var repository = RepositoryWithTopic().Branch("release");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.BranchSwitch);
        harness.Type("rel");
        Assert.Contains("release (1/1)", harness.Branches.StatusLine, StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("release", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenTheCurrentBranch_WhenSwitchingToIt_ThenTheNoticeSaysAlreadyThere()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.BranchSwitch);
        harness.Type("main");
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("Already on main", harness.Branches.Notice);
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenABranchInAnotherWorktree_WhenSwitching_ThenItIsRefusedWithoutRunningGit()
    {
        using var repository = RepositoryWithTopic();
        var worktree = repository.AddWorktree("wt-topic", "topic");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.BranchSwitch);
        harness.Type("topic");
        harness.Run(ActionIds.InputAccept);

        Assert.Contains("is checked out in", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Contains("topic", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Equal(0, harness.Refreshes);
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.True(Directory.Exists(worktree));
    }

    [Fact]
    public async Task GivenARemoteBranchWithoutLocalTwin_WhenSwitching_ThenATrackingBranchIsCreated()
    {
        using var repository = RepositoryWithTopic();
        repository.AddOrigin();
        repository.Git("push", "-q", "origin", "topic");
        repository.Git("branch", "-D", "topic");
        repository.Git("fetch", "-q", "origin");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.BranchSwitch);
        harness.Type("origin/topic");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("topic", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("origin/topic", repository.Git("rev-parse", "--abbrev-ref", "topic@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenAGraphCommit_WhenSwitchingToTheDetachedEntry_ThenHeadIsDetachedThere()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, first));

        harness.Run(ActionIds.BranchSwitch);
        harness.Type("detached");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal(first, repository.Head);
        Assert.Equal(string.Empty, repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenADirtyTreeThatConflicts_WhenSwitching_ThenGitsMessageIsShownAndTheEditSurvives()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("shared.txt", "1\n"))
            .CheckoutNewBranch("topic")
            .Commit("topic", new TestFile("shared.txt", "topic\n"))
            .Checkout("main");
        repository.WriteFile("shared.txt", "local edit\n");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, null));

        harness.Run(ActionIds.BranchSwitch);
        harness.Type("topic");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not switch", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("local edit\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "shared.txt")));
    }

    [Fact]
    public async Task GivenABranchRow_WhenRenaming_ThenThePromptIsPrefilledAndTheBranchMoves()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchRename);
        Assert.Equal("rename topic: topic_", harness.Branches.StatusLine);
        harness.Type("-2");
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic-2").Trim());
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenATagRow_WhenRenaming_ThenNoPromptOpens()
    {
        using var repository = RepositoryWithTopic().Tag("v1");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.Tag, "v1", repository.Head));

        harness.Run(ActionIds.BranchRename);

        Assert.Equal("Select a local branch first", harness.Branches.Notice);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
    }

    [Fact]
    public async Task GivenAGraphCommitWithTwoBranches_WhenRenaming_ThenTheNoticeAsksForTheSidebar()
    {
        using var repository = RepositoryWithTopic().Branch("second");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));

        harness.Run(ActionIds.BranchRename);

        Assert.Contains("branches point here", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
    }

    [Fact]
    public async Task GivenAMergedBranch_WhenDeleting_ThenOneDialogNamesItAndYesDeletesIt()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchDelete);

        var prompt = Assert.Single(harness.Prompts);
        Assert.Contains("topic", prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains($"git branch topic {repository.Head}", prompt.Consequence, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());

        harness.Answer();
        await harness.FinishAsync();

        Assert.Single(harness.Prompts);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
        Assert.Contains("Deleted branch topic", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnUnmergedBranch_WhenDeleting_ThenASecondDialogIsNeededAndDecliningKeepsTheBranch()
    {
        using var repository = RepositoryWithTopic()
            .CheckoutNewBranch("wip")
            .Commit("only here", new TestFile("only.txt", "x"))
            .Checkout("main");
        var tip = repository.Git("rev-parse", "wip").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "wip", tip));

        harness.Run(ActionIds.BranchDelete);
        harness.Answer();
        await harness.PumpUntilAsync(() => harness.Prompts.Count == 2);

        Assert.Equal("Delete unmerged branch", harness.Prompts[1].Title);
        Assert.Contains("unreachable", string.Join(' ', harness.Prompts[1].Lines), StringComparison.Ordinal);
        Assert.Contains($"git branch wip {tip}", harness.Prompts[1].Consequence, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "wip").Trim());
        Assert.Equal(0, harness.Refreshes);
    }

    [Fact]
    public async Task GivenAnUnmergedBranch_WhenBothDialogsAreAnsweredYes_ThenItIsDeletedAndTheTipIsReported()
    {
        using var repository = RepositoryWithTopic()
            .CheckoutNewBranch("wip")
            .Commit("only here", new TestFile("only.txt", "x"))
            .Checkout("main");
        var tip = repository.Git("rev-parse", "wip").Trim();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "wip", tip));

        harness.Run(ActionIds.BranchDelete);
        harness.Answer();
        await harness.PumpUntilAsync(() => harness.Prompts.Count == 2);
        harness.Answer();
        await harness.FinishAsync();

        Assert.Equal(string.Empty, repository.Git("branch", "--list", "wip").Trim());
        Assert.Contains(tip, harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(tip, repository.Git("rev-parse", tip).Trim());
    }

    [Fact]
    public async Task GivenTheCurrentBranch_WhenDeleting_ThenItIsRefusedWithoutADialog()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "main", repository.Head));

        harness.Run(ActionIds.BranchDelete);

        Assert.Contains("main is checked out", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
    }

    [Fact]
    public async Task GivenABranchInAnotherWorktree_WhenDeleting_ThenItIsRefusedWithoutADialog()
    {
        using var repository = RepositoryWithTopic();
        repository.AddWorktree("wt-topic", "topic");
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchDelete);

        Assert.Contains("topic is checked out in", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Prompts);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenAScreenTooSmallForTheDialog_WhenDeleting_ThenNothingIsDeleted()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.CanShowDialog = false;
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchDelete);

        Assert.Contains("nothing was deleted", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenAnotherTabBecomesActive_WhenAcceptingThePrompt_ThenTheNameIsNotUsed()
    {
        using var repository = RepositoryWithTopic();
        using var other = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(null, null, repository.Head));
        harness.Run(ActionIds.BranchCreate);
        harness.Type("lost");

        await harness.OpenTabAsync(other);
        var handled = harness.Branches.TryExecute(ActionIds.InputAccept, null);

        Assert.False(handled);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "lost").Trim());
        Assert.Equal(string.Empty, other.Git("branch", "--list", "lost").Trim());
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenALocalBranchWithoutUpstream_WhenSettingOne_ThenThePickedRemoteBranchIsTracked()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "lonely", repository.Head));

        harness.Run(ActionIds.BranchSetUpstream);
        Assert.Contains("upstream of lonely", harness.Branches.StatusLine, StringComparison.Ordinal);
        harness.Type("main");
        Assert.Contains("origin/main (1/1)", harness.Branches.StatusLine, StringComparison.Ordinal);
        harness.Run(ActionIds.InputAccept);
        await harness.FinishAsync();

        Assert.Equal("origin/main", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
        Assert.Equal("lonely now tracks origin/main", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenNoMatchingRemoteBranch_WhenAcceptingTheUpstreamPrompt_ThenNothingChanges()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "lonely", repository.Head));

        harness.Run(ActionIds.BranchSetUpstream);
        harness.Type("zzz");
        harness.Run(ActionIds.InputAccept);

        Assert.Equal("No matching remote branch", harness.Branches.Notice);
        Assert.Equal(0, harness.Refreshes);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenARepositoryWithoutRemoteBranches_WhenSettingUpstream_ThenNoPromptOpens()
    {
        using var repository = RepositoryWithTopic();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchSetUpstream);

        Assert.Contains("no remote branch", harness.Branches.Notice, StringComparison.Ordinal);
        Assert.Equal(string.Empty, harness.Branches.StatusLine);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public async Task GivenATrackingBranch_WhenUnsettingTheUpstream_ThenTheConfigIsGoneAndTheRestoreCommandIsShown()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "topic", repository.Head));

        harness.Run(ActionIds.BranchUnsetUpstream);
        await harness.FinishAsync();

        Assert.Equal(string.Empty, repository.Git("for-each-ref", "--format=%(upstream)", "refs/heads/topic").Trim());
        Assert.Contains(
            "git branch --set-upstream-to=origin/topic topic",
            harness.Statuses.Log.Latest!.Text,
            StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenABranchWithoutUpstream_WhenUnsetting_ThenGitIsNotRun()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var harness = await Harness.LoadAsync(repository);
        harness.Select(new BranchSelection(SidebarRowKind.LocalBranch, "lonely", repository.Head));

        harness.Run(ActionIds.BranchUnsetUpstream);

        Assert.Equal("lonely has no upstream", harness.Branches.Notice);
        Assert.Equal(0, harness.Refreshes);
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);
        private readonly List<Action> _answers = [];
        private readonly List<TabSession> _sessions = [];
        private BranchSelection _selection = new(null, null, null);

        private Harness()
        {
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Branches = new BranchActions(
                _host,
                TestGit.Services.Branches,
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
                () => _selection);
        }

        public BranchActions Branches { get; }

        public StatusChannel Statuses { get; } = new();

        public List<ConfirmPrompt> Prompts { get; } = [];

        public int Refreshes { get; private set; }

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public bool CanShowDialog { get; set; } = true;

        private UiPump Pump { get; }

        public static async Task<Harness> LoadAsync(TestRepository repository)
        {
            var harness = new Harness();
            await harness.OpenTabAsync(repository);
            return harness;
        }

        public async Task OpenTabAsync(TestRepository repository)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            _host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = _host.Workspace.ActiveTabId!.Value;
            var session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
            _sessions.Add(session);
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                session,
                _host,
                new StatusChannel(),
                InlineUi.Post,
                session.Cancellation.Token);
        }

        public TabId TabId => _host.Workspace.ActiveTabId!.Value;

        public async Task LoadOperationAsync()
        {
            var token = TestContext.Current.CancellationToken;
            var location = _host.Workspace.ActiveTab!.Location;
            var operation = new RepositoryOperationDetector().Detect(location);
            var info = await new OperationInfoReader().ReadAsync(location, operation, token);
            _host.Dispatch(new TabCommand(TabId, new OperationLoadedCommand(info)));
        }

        public void Select(BranchSelection selection) => _selection = selection;

        public void Run(ActionId action) => Assert.True(Branches.TryExecute(action, null), action.Value);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Branches.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

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
