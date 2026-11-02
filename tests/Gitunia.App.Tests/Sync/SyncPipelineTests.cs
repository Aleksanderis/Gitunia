using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Sync;

// Fetch, pull and push end to end: command -> reducer -> effect -> runner -> handler -> real git -> result command.
public class SyncPipelineTests
{
    [Fact]
    public async Task GivenAFetch_WhenItFinishes_ThenGitFetchedAndTheTabIsFreeAgain()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var theirs = PushFromAnotherClone(repository, "topic");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Fetch));
        Assert.True(pipeline.IsRunning);
        Assert.Equal("Fetching...", pipeline.Tab.Sync!.ProgressLine);
        await pipeline.UntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(theirs, repository.Git("rev-parse", "origin/topic").Trim());
        Assert.Equal("Fetched the remote of the current branch", pipeline.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Info, pipeline.Statuses.Log.Latest.Severity);
        Assert.Null(pipeline.Tab.Sync);
    }

    [Fact]
    public async Task GivenAPull_WhenItFinishes_ThenTheBranchFastForwards()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var theirs = PushFromAnotherClone(repository, "topic");
        repository.Git("fetch", "-q", "origin");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Pull));
        await pipeline.UntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(theirs, repository.Git("rev-parse", "HEAD").Trim());
        Assert.Equal("Pulled (fast-forward)", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAFetchThatNeedsCredentials_WhenItFails_ThenTheRunnerAsksTheShellForTheTerminalRetry()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var writer = new FakeRemoteWriter
        {
            Failure = new GitCommandFailedException(["fetch"], 128, "Permission denied (publickey)."),
        };
        using var pipeline = await Pipeline.OpenAsync(repository, writer);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Fetch));
        await pipeline.UntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
        Assert.Equal(["Fetch|fetch --prune"], pipeline.Shell.Retries);
    }

    [Fact]
    public async Task GivenARunningFetch_WhenCancelling_ThenGitStopsAndTheCancelIsWarnedAbout()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var writer = new FakeRemoteWriter { Blocks = true };
        using var pipeline = await Pipeline.OpenAsync(repository, writer);
        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Fetch));
        await writer.Started;

        pipeline.Dispatch(new SyncCancelRequested(pipeline.TabId));
        Assert.EndsWith("(cancelling)", pipeline.Tab.Sync!.ProgressLine, StringComparison.Ordinal);
        await pipeline.UntilAsync(() => !pipeline.IsRunning);

        Assert.Equal("Fetch cancelled", pipeline.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Warning, pipeline.Statuses.Log.Latest.Severity);
        Assert.Empty(pipeline.Shell.Retries);
        Assert.Null(pipeline.Tab.Sync);
    }

    [Fact]
    public async Task GivenProgress_WhenRunning_ThenTheLineShowsGitsLatestReportAndNoMessageIsPosted()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var writer = new FakeRemoteWriter { Blocks = true };
        writer.Reports.Add(new RemoteProgress("Receiving objects", 45));
        using var pipeline = await Pipeline.OpenAsync(repository, writer);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Fetch));
        await pipeline.UntilAsync(() => pipeline.Tab.Sync?.Latest is not null);

        Assert.Equal("Fetching: Receiving objects 45%", pipeline.Tab.Sync!.ProgressLine);
        Assert.Null(pipeline.Statuses.Log.Latest);
        writer.Release();
        await pipeline.UntilAsync(() => !pipeline.IsRunning);
    }

    [Fact]
    public async Task GivenABranchWithoutUpstreamAndOneRemote_WhenPushing_ThenItIsPushedWithoutAskingForTheRemote()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.Null(pipeline.Host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
        Assert.Equal("origin/lonely", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "lonely"]).Trim());
        Assert.Contains("set it as its upstream", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTwoRemotes_WhenPushingAFirstTime_ThenTheRemoteIsAskedOriginFirstAndTheUpstreamIsSet()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        repository.Git("remote", "add", "second", repository.OriginPath);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Prompt is not null);
        var prompt = pipeline.Host.State.Shell.Prompt!;
        Assert.Equal("Remote to push lonely to", prompt.Context);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.Equal("origin/lonely", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenTheFirstPushPrompt_WhenCancelling_ThenNothingIsPushed()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        repository.Git("remote", "add", "second", repository.OriginPath);
        using var pipeline = await Pipeline.OpenAsync(repository);
        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Prompt is not null);

        pipeline.Dispatch(new PromptCancelledCommand());

        Assert.Null(pipeline.Host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
        Assert.DoesNotContain("lonely", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheRemoteHasADivergedBranchOfTheSameName_WhenPushingAFirstTime_ThenTheRejectionOffersTheForcePush()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var theirs = PushFromAnotherClone(repository, "lonely", createBranch: true);
        repository.Git("fetch", "-q", "origin");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        Assert.StartsWith("Push failed", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        var confirm = pipeline.Host.State.Shell.Confirm!.Prompt;
        Assert.Equal("Force push", confirm.Title);
        Assert.Contains("origin/lonely", confirm.Lines[0], StringComparison.Ordinal);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "lonely"]).Trim());

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.UntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "lonely"]).Trim());
        Assert.Equal("origin/lonely", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenTheRemoteBranchOfTheSameNameIsAheadOfTheLocalOne_WhenPushingAFirstTime_ThenNoForcePushIsOffered()
    {
        using var repository = TestRepositories.WithOrigin().CheckoutNewBranch("older");
        var theirs = PushFromAnotherClone(repository, "older", createBranch: true);
        repository.Git("fetch", "-q", "origin");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(
            () => pipeline.Statuses.Log.Latest?.Text.Contains("pull or rebase", StringComparison.Ordinal) == true);

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "older"]).Trim());
    }

    [Fact]
    public async Task GivenARewrittenBranch_WhenForcePushing_ThenTheConfirmNamesTheRemoteBranchAndTheLeaseOverwritesIt()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.PushForce));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        var confirm = pipeline.Host.State.Shell.Confirm!.Prompt;
        Assert.Contains("origin/topic", confirm.Lines[0], StringComparison.Ordinal);
        Assert.Contains(seen[..7], confirm.Lines[0], StringComparison.Ordinal);
        Assert.Contains($"{seen}:topic", confirm.Consequence, StringComparison.Ordinal);
        Assert.Equal(seen, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
        Assert.StartsWith("Force pushed topic to origin/topic", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheRemoteMovedMeanwhile_WhenForcePushing_ThenTheLeaseIsRejectedAndTheirCommitSurvives()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        using var pipeline = await Pipeline.OpenAsync(repository);
        var theirs = PushFromAnotherClone(repository, "topic");

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.PushForce));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Error, latest.Severity);
        Assert.StartsWith("Push failed", latest.Text, StringComparison.Ordinal);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
        Assert.Null(pipeline.Host.State.Shell.Confirm);
    }

    [Fact]
    public async Task GivenTheTabIsClosedWhileTheForceDialogIsOpen_ThenYPushesNothing()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        using var pipeline = await Pipeline.OpenAsync(repository);
        var tabId = pipeline.TabId;
        pipeline.Dispatch(new SyncRequested(tabId, SyncKind.PushForce));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        pipeline.Dispatch(new CloseTabCommand(tabId));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(seen, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenARewrittenBranch_WhenPushingPlainly_ThenTheRejectionOffersTheForcePush()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        repository.Git("commit", "--amend", "-q", "-m", "Rewritten");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        var confirm = pipeline.Host.State.Shell.Confirm!.Prompt;
        Assert.Equal("Force push", confirm.Title);
        Assert.Contains($"{seen}:topic", confirm.Consequence, StringComparison.Ordinal);
        Assert.Equal(seen, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.UntilAsync(() => !pipeline.IsRunning);

        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenABranchOnlyBehindItsUpstream_WhenPushingPlainly_ThenNoForcePushIsOffered()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var seen = repository.Git("rev-parse", "origin/topic").Trim();
        repository.Git("reset", "--hard", "-q", "HEAD~1");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.StartsWith("Push failed", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(seen, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenTheRemoteHasUnfetchedCommits_WhenPushingPlainly_ThenNoForcePushIsOffered()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 0);
        using var pipeline = await Pipeline.OpenAsync(repository);
        var theirs = PushFromAnotherClone(repository, "topic");

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.StartsWith("Push failed", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(theirs, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenACleanFastForwardPush_WhenPushingPlainly_ThenNoForcePushIsOffered()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 0);
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.Push));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(repository.Head, repository.GitIn(repository.OriginPath, ["rev-parse", "topic"]).Trim());
    }

    [Fact]
    public async Task GivenABranchWithoutUpstream_WhenForcePushing_ThenItIsRefusedWithoutAConfirm()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncRequested(pipeline.TabId, SyncKind.PushForce));

        Assert.Equal("lonely has no upstream: push it first", pipeline.Statuses.Log.Latest!.Text);
        Assert.Null(pipeline.Host.State.Shell.Confirm);
    }

    [Fact]
    public async Task GivenARemoteBranch_WhenDeletingIt_ThenTheBranchPickerAndTheConfirmComeFirst()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var tip = repository.Git("rev-parse", "origin/topic").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncDeleteRequested(pipeline.TabId, new SyncTarget(null, null, null)));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Prompt is not null);
        Assert.Equal("Branch to delete on origin", pipeline.Host.State.Shell.Prompt!.Context);
        foreach (var character in "topic")
        {
            pipeline.Dispatch(new PromptKeyPressedCommand(new KeyEvent(character.ToString())));
        }

        pipeline.Dispatch(new PromptAcceptedCommand());

        var confirm = pipeline.Host.State.Shell.Confirm!.Prompt;
        Assert.Contains("topic", confirm.Lines[0], StringComparison.Ordinal);
        Assert.Contains("origin", confirm.Lines[0], StringComparison.Ordinal);
        Assert.Contains($"git push origin {tip}:refs/heads/topic", confirm.Consequence, StringComparison.Ordinal);
        Assert.Contains("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        await pipeline.UntilAsync(() => pipeline.Statuses.Log.Latest is not null && !pipeline.IsRunning);

        Assert.DoesNotContain("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
        Assert.Equal(tip, repository.Git("rev-parse", "topic").Trim());
        Assert.Contains(tip, pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheCursorOnARemoteBranchRow_WhenDeleting_ThenOnlyTheConfirmComesFirst()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var tip = repository.Git("rev-parse", "origin/topic").Trim();
        using var pipeline = await Pipeline.OpenAsync(repository);
        var target = new SyncTarget(SidebarRowKind.RemoteBranch, "origin/topic", tip);

        pipeline.Dispatch(new SyncDeleteRequested(pipeline.TabId, target));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        var confirm = pipeline.Host.State.Shell.Confirm!.Prompt;
        Assert.Contains("topic", confirm.Lines[0], StringComparison.Ordinal);
        Assert.Null(pipeline.Host.State.Shell.Prompt);
        Assert.Equal(KeyMode.Confirm, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenAGraphCommitWithOnlyARemoteBranch_WhenDeleting_ThenOnlyTheConfirmComesFirst()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var tip = repository.Git("rev-parse", "origin/topic").Trim();
        repository.Git("checkout", "-q", "main");
        repository.Git("branch", "-D", "topic");
        using var pipeline = await Pipeline.OpenAsync(repository);

        pipeline.Dispatch(new SyncDeleteRequested(pipeline.TabId, new SyncTarget(null, null, tip)));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        Assert.Contains("topic", pipeline.Host.State.Shell.Confirm!.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Equal(KeyMode.Confirm, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenTheDeleteConfirmIsDeclined_WhenAnswered_ThenTheRemoteBranchStays()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var pipeline = await Pipeline.OpenAsync(repository);
        var target = new SyncTarget(SidebarRowKind.RemoteBranch, "origin/topic", null);
        pipeline.Dispatch(new SyncDeleteRequested(pipeline.TabId, target));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.False(pipeline.IsRunning);
        Assert.Contains("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheTabIsClosedWhileTheDeleteDialogIsOpen_ThenYDeletesNothing()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        using var pipeline = await Pipeline.OpenAsync(repository);
        var tabId = pipeline.TabId;
        var target = new SyncTarget(SidebarRowKind.RemoteBranch, "origin/topic", null);
        pipeline.Dispatch(new SyncDeleteRequested(tabId, target));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Confirm is not null);

        pipeline.Dispatch(new CloseTabCommand(tabId));
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Contains("topic", repository.GitIn(repository.OriginPath, ["branch"]), StringComparison.Ordinal);
    }

    private static string PushFromAnotherClone(TestRepository repository, string branch, bool createBranch = false)
    {
        var root = Path.GetDirectoryName(repository.OriginPath)!;
        var clone = Path.Combine(root, "other-clone");
        if (createBranch)
        {
            repository.GitIn(root, ["clone", "-q", repository.OriginPath, clone]);
            repository.GitIn(clone, ["checkout", "-q", "-b", branch]);
        }
        else
        {
            repository.GitIn(root, ["clone", "-q", "--branch", branch, repository.OriginPath, clone]);
        }

        File.WriteAllText(Path.Combine(clone, "theirs.txt"), "t\n");
        repository.GitIn(clone, ["add", "-A"]);
        repository.GitIn(clone, ["commit", "-q", "-m", "Theirs"]);
        repository.GitIn(clone, ["push", "-q", "origin", branch]);

        return repository.GitIn(clone, ["rev-parse", "HEAD"]).Trim();
    }

    private sealed class Pipeline : IDisposable
    {
        private readonly EffectRunner<TabSession> _runner;
        private readonly UiPump _pump;

        private Pipeline(
            Dispatcher host,
            EffectRunner<TabSession> runner,
            UiPump pump,
            StatusReporter statuses,
            RecordingShell shell)
        {
            Host = host;
            _runner = runner;
            _pump = pump;
            Statuses = statuses;
            Shell = shell;
        }

        public Dispatcher Host { get; }

        public StatusReporter Statuses { get; }

        public RecordingShell Shell { get; }

        public TabId TabId => Host.Workspace.ActiveTabId!.Value;

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public bool IsRunning => Host.Workspace.FindTab(TabId)!.Running.IsRunning(RunningOperationKind.Sync);

        public static async Task<Pipeline> OpenAsync(
            TestRepository repository,
            IRemoteWriter? writer = null)
        {
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty, pump);
            var statuses = new StatusReporter(host);
            var shell = new RecordingShell();
            var services = writer is null ? TestGit.Services : TestGit.Services with { Remote = writer };
            var runner = new EffectRunner<TabSession>(
                services,
                host,
                pump,
                new TabSessionFactory(),
                statuses,
                watcherOptions: TestGit.QuietWatcherOptions,
                shell: shell);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pipeline = new Pipeline(host, runner, pump, statuses, shell);
            var session = runner.SessionOf(pipeline.TabId)!;
            await pump.RunUntilAsync(() => !session.IsLoading && host.Workspace.FindTab(pipeline.TabId)!.Refs.Length > 0);

            return pipeline;
        }

        public void Dispatch(AppCommand command) => Host.Dispatch(command);

        public Task UntilAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose() => _runner.Dispose();
    }
}
