using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Branches;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Branches;

// The branch feature end to end: command -> reducer -> effect -> runner -> handler -> real git -> result -> refresh.
public class BranchPipelineTests
{
    [Fact]
    public async Task GivenAGraphCommit_WhenCreatingWithoutCheckoutThroughThePrompt_ThenGitHasItAndTheCursorFollows()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new BranchCreateRequested(pipeline.TabId, new BranchTarget(null, null, first), false));
        Assert.Equal("New branch", pipeline.Prompt!.Title);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Type("old");
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.True(pipeline.IsBranchRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && pipeline.HasBranch("old"));

        Assert.Equal(first, repository.Git("rev-parse", "old").Trim());
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(ObjectIdOf(first), pipeline.SelectedCommit);
        Assert.Contains("Created branch old at", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenAccepted_ThenTheErrorIsShownAndTheTabIsFreeAgain()
    {
        using var repository = RepositoryWithTopic();
        using var pipeline = await Pipeline.OpenAsync(repository);

        var target = new BranchTarget(null, null, repository.Head);

        pipeline.Dispatch(new BranchCreateRequested(pipeline.TabId, target, true));
        pipeline.Type("bad name");
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning);

        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
        Assert.Contains("not a valid branch name", pipeline.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "bad name").Trim());
    }

    [Fact]
    public async Task GivenAFilterText_WhenSwitchingThroughThePicker_ThenTheBestMatchIsCheckedOutAndTheCursorFollows()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        repository.Git("branch", "release", first);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new BranchSwitchRequested(pipeline.TabId, new BranchTarget(null, null, null)));
        pipeline.Type("rel");
        Assert.Equal("release", Assert.Single(pipeline.Prompt!.Picker!.Candidates).Text);
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && pipeline.IsCurrent("release"));

        Assert.Equal("release", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(ObjectIdOf(first), pipeline.SelectedCommit);
    }

    [Fact]
    public async Task GivenARemoteBranchHighlightedInThePicker_WhenAcceptingWithoutTyping_ThenItIsCheckedOutLocally()
    {
        using var repository = RepositoryWithTopic();
        repository.AddOrigin();
        repository.Git("push", "-q", "origin", "topic");
        repository.Git("branch", "-D", "topic");
        repository.Git("fetch", "-q", "origin");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new BranchSwitchRequested(pipeline.TabId, new BranchTarget(null, null, null)));
        pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent(KeyNames.Down)));
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && pipeline.IsCurrent("topic"));

        Assert.Equal("origin/topic", repository.Git("rev-parse", "--abbrev-ref", "topic@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenABranch_WhenRenamingThroughThePrompt_ThenThePromptIsPrefilledAndTheBranchMoves()
    {
        using var repository = RepositoryWithTopic();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new BranchRenameRequested(
            pipeline.TabId,
            new BranchTarget(SidebarRowKind.LocalBranch, "topic", repository.Head)));
        Assert.Equal("topic", pipeline.Prompt!.Text);
        pipeline.Type("-2");
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && pipeline.HasBranch("topic-2"));

        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
        Assert.Equal(ObjectIdOf(repository.Head), pipeline.SelectedCommit);
    }

    [Fact]
    public async Task GivenAMergedBranch_WhenDeletingThroughTheDialog_ThenOneYDeletesItAndTheCursorGoesToHead()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        repository.Git("branch", "old", first);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new BranchDeleteRequested(
            pipeline.TabId,
            new BranchTarget(SidebarRowKind.LocalBranch, "old", first)));
        Assert.Contains("old", pipeline.Host.State.Shell.Confirm!.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "old").Trim());
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && !pipeline.HasBranch("old"));

        Assert.Equal(string.Empty, repository.Git("branch", "--list", "old").Trim());
        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(ObjectIdOf(repository.Head), pipeline.SelectedCommit);
        Assert.Contains("Deleted branch old", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnUnmergedBranch_WhenBothDialogsAreAnsweredYes_ThenItIsDeletedAndTheTipIsReported()
    {
        using var repository = RepositoryWithTopic()
            .CheckoutNewBranch("wip")
            .Commit("only here", new TestFile("only.txt", "x"))
            .Checkout("main");
        var tip = repository.Git("rev-parse", "wip").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);

        var target = new BranchTarget(SidebarRowKind.LocalBranch, "wip", tip);

        pipeline.Dispatch(new BranchDeleteRequested(pipeline.TabId, target));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        Assert.Equal("Delete unmerged branch", pipeline.Host.State.Shell.Confirm!.Prompt.Title);
        Assert.False(pipeline.IsBranchRunning);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "wip").Trim());
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && !pipeline.HasBranch("wip"));

        Assert.Equal(string.Empty, repository.Git("branch", "--list", "wip").Trim());
        Assert.Contains(tip, pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnUnmergedBranch_WhenTheSecondDialogIsDeclined_ThenTheBranchStaysAndTheTabIsFree()
    {
        using var repository = RepositoryWithTopic()
            .CheckoutNewBranch("wip")
            .Commit("only here", new TestFile("only.txt", "x"))
            .Checkout("main");
        var tip = repository.Git("rev-parse", "wip").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);

        var target = new BranchTarget(SidebarRowKind.LocalBranch, "wip", tip);

        pipeline.Dispatch(new BranchDeleteRequested(pipeline.TabId, target));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.False(pipeline.IsBranchRunning);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "wip").Trim());
    }

    [Fact]
    public async Task GivenATooSmallScreen_WhenDeleting_ThenNoDialogOpensAndNothingIsDeleted()
    {
        using var repository = RepositoryWithTopic();
        using var pipeline = await Pipeline.OpenAsync(repository);
        pipeline.Dispatch(new ScreenResizedCommand(new ScreenExtent(1, 1), pipeline.Host.Clock.GetUtcNow()));

        pipeline.Dispatch(new BranchDeleteRequested(
            pipeline.TabId,
            new BranchTarget(SidebarRowKind.LocalBranch, "topic", repository.Head)));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Contains("nothing was deleted", pipeline.Statuses.Current!.Text, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenATrackingBranchBehind_WhenFastForwarding_ThenItMovesAtOnceAndTheRefsShowIt()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 2);
        repository.Git("switch", "main");
        repository.Git("fetch", "-q");
        var old = repository.Git("rev-parse", "refs/heads/topic").Trim();
        var tip = repository.Git("rev-parse", "refs/remotes/origin/topic").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new BranchFastForwardRequested(
            pipeline.TabId,
            new BranchTarget(SidebarRowKind.LocalBranch, "topic", old)));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsBranchRunning && pipeline.TipOf("topic") == tip);

        Assert.Equal(tip, repository.Git("rev-parse", "refs/heads/topic").Trim());
        var text = pipeline.Statuses.Log.Latest!.Text;
        Assert.Contains($"git update-ref refs/heads/topic {old}", text, StringComparison.Ordinal);
    }

    private static TestRepository RepositoryWithTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"))
            .Branch("topic");

    private static ObjectId ObjectIdOf(string hash)
    {
        Assert.True(ObjectId.TryParse(hash, out var id));

        return id;
    }

    private sealed class Pipeline : IDisposable
    {
        private readonly EffectRunner<TabSession> _runner;

        private Pipeline(Dispatcher host, EffectRunner<TabSession> runner, UiPump pump, StatusReporter statuses)
        {
            Host = host;
            _runner = runner;
            Pump = pump;
            Statuses = statuses;
        }

        public Dispatcher Host { get; }

        public UiPump Pump { get; }

        public StatusReporter Statuses { get; }

        public TabId TabId => Host.Workspace.ActiveTabId!.Value;

        public bool HasRefs => Host.Workspace.FindTab(TabId)!.Refs.Length > 0;

        public OpenPrompt? Prompt => Host.State.Shell.Prompt;

        public bool IsBranchRunning => Host.Workspace.FindTab(TabId)!.Running.IsRunning(RunningOperationKind.Branches);

        public ObjectId? SelectedCommit => Host.Workspace.FindTab(TabId)!.View.SelectedCommit;

        public static async Task<Pipeline> OpenAsync(TestRepository repository)
        {
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty, pump);
            var statuses = new StatusReporter(host);
            var runner = new EffectRunner<TabSession>(
                TestGit.Services,
                host,
                pump,
                new TabSessionFactory(),
                statuses,
                watcherOptions: TestGit.QuietWatcherOptions);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pipeline = new Pipeline(host, runner, pump, statuses);
            var session = runner.SessionOf(pipeline.TabId)!;
            await pump.RunUntilAsync(() => !session.IsLoading && pipeline.HasRefs);

            return pipeline;
        }

        public void Dispatch(AppCommand command) => Host.Dispatch(command);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Host.Dispatch(new PromptKeyPressedCommand(new KeyEvent(character.ToString())));
            }
        }

        public bool HasBranch(string name) => Host.Workspace.FindTab(TabId)!.Refs.Any(reference =>
            reference.Kind == RefKind.LocalBranch && reference.Name == name);

        public bool IsCurrent(string name) => Host.Workspace.FindTab(TabId)!.Refs.Any(reference =>
            reference is { Kind: RefKind.LocalBranch, IsCurrent: true } && reference.Name == name);

        public string? TipOf(string name) => Host.Workspace.FindTab(TabId)!.Refs
            .FirstOrDefault(reference => reference.Kind == RefKind.LocalBranch && reference.Name == name)
            ?.CommitHash;

        public void Dispose() => _runner.Dispose();
    }
}
