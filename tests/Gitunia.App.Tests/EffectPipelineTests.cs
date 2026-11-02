using System.Collections.Immutable;
using Gitunia.App.Background;
using Gitunia.App.Settings;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Config;
using Gitunia.Core.Details;
using Gitunia.Core.DiffPane;
using Gitunia.Core.FileTree;
using Gitunia.Core.Lists;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Effects;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Running;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Prompts;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Tags;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.ExternalTools;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests;

// The pilot feature (tags) end to end: command -> reducer -> effect -> runner -> handler -> real git -> result command.
public class EffectPipelineTests
{
    [Fact]
    public async Task GivenACommit_WhenCreatingATagThroughCommands_ThenGitHasItAndTheStateIsFreeAgain()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new TagCreateRequested(pipeline.TabId, new TagTarget(null, null, repository.Head)));
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        Assert.Equal("New tag", pipeline.Prompt!.Title);
        pipeline.Type("v1");
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.Equal("Tag message", pipeline.Prompt!.Title);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Dispatch(new PromptAcceptedCommand());
        Assert.Null(pipeline.Prompt);
        Assert.True(pipeline.IsTagRunning);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsTagRunning);

        Assert.Equal(repository.Head, repository.Git("rev-parse", "v1").Trim());
        Assert.Null(pipeline.Prompt);
        Assert.Contains("lightweight tag v1", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenATab_WhenRenamingItThroughThePromptCommands_ThenTheTitleChangesAndTheModeGoesBack()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        var original = pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Title;

        pipeline.Dispatch(new TabRenameRequested(pipeline.TabId));
        Assert.Equal(original, pipeline.Prompt!.Text);
        Assert.Equal(KeyMode.Insert, pipeline.Host.State.Shell.Mode);
        pipeline.Type("-2");
        pipeline.Dispatch(new PromptAcceptedCommand());

        Assert.Equal($"{original}-2", pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Title);
        Assert.Null(pipeline.Prompt);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
        Assert.Equal($"tab is now {original}-2", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenATag_WhenDeletingItThroughCommands_ThenTheConfirmCarriesTheCommandThatDeletesIt()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        var target = new TagTarget(SidebarRowKind.Tag, "v1", repository.Head);

        pipeline.Dispatch(new TagDeleteRequested(pipeline.TabId, target));
        var confirm = pipeline.Host.State.Shell.Confirm!;
        Assert.Equal("Delete tag", confirm.Prompt.Title);
        Assert.Equal(KeyMode.Confirm, pipeline.Host.State.Shell.Mode);
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));
        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsTagRunning);

        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
        Assert.Contains("Deleted tag v1", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenATooSmallScreen_WhenDeletingATagThroughCommands_ThenNoDialogOpensAndTheNoticeIsShown()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new ScreenResizedCommand(new ScreenExtent(80, 5), pipeline.Host.Clock.GetUtcNow()));

        pipeline.Dispatch(new TagDeleteRequested(
            pipeline.TabId,
            new TagTarget(SidebarRowKind.Tag, "v1", repository.Head)));

        Assert.Null(pipeline.Host.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
        Assert.Equal("The screen is too small to ask: nothing was changed", pipeline.Statuses.Current?.Text);
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenARunningTagOperation_WhenAnotherIsRequested_ThenItIsRefusedAsBusy()
    {
        using var repository = TestRepositories.Linear();
        var writer = new BlockingTagWriter();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services with { Tags = writer }, repository);
        pipeline.Dispatch(new TagDeleteConfirmed(pipeline.TabId, "v1", repository.Head));
        Assert.True(pipeline.IsTagRunning);

        pipeline.Dispatch(new TagCreateRequested(pipeline.TabId, new TagTarget(null, null, repository.Head)));

        Assert.Null(pipeline.Prompt);
        Assert.Equal(TagReducer.BusyNotice, pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenARunningTagOperation_WhenItsTabIsClosed_ThenItsWorkIsCancelled()
    {
        using var repository = TestRepositories.Linear();
        var writer = new BlockingTagWriter();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services with { Tags = writer }, repository);
        pipeline.Dispatch(new TagDeleteConfirmed(pipeline.TabId, "v1", repository.Head));
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        pipeline.Dispatch(new CloseTabCommand(pipeline.TabId));

        Assert.True(writer.Token.IsCancellationRequested);
        await pipeline.Pump.RunUntilAsync(() => writer.Finished);
    }

    [Fact]
    public async Task GivenAHandlerThatThrows_WhenRunning_ThenAnErrorIsPostedInsteadOfTheExceptionEscaping()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Shell.RunRetryFailure = new InvalidOperationException("boom");
        pipeline.Dispatch(new ConfirmRequestedCommand(
            new ConfirmPrompt("Boom", ["Run it?"], "No undo.", "Run"),
            new RetryInTerminalConfirmed(pipeline.TabId, "Boom", []),
            null));

        pipeline.Dispatch(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.StartsWith("Internal error (InvalidOperationException):", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest.Severity);
    }

    [Fact]
    public async Task GivenATab_WhenAskingForTheMergetool_ThenTheShellIsHandedTheLaunchForThatTab()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new ExternalToolRequested(ExternalToolKind.Mergetool, null));

        var launch = Assert.Single(pipeline.Shell.Tools);
        Assert.Equal(pipeline.TabId, launch.TabId);
        Assert.Equal(ExternalToolKind.Mergetool, launch.Kind);
    }

    [Fact]
    public async Task GivenAnOpenQuickJump_WhenALabelIsPressed_ThenTheShellIsAskedToJumpToThatRow()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new QuickJumpOpenRequested(pipeline.TabId, PaneKind.Graph, true, null));
        ImmutableArray<JumpLabel> labels = [new(1, 0, 1, 'q')];
        pipeline.Dispatch(new QuickJumpKeyPressed(new KeyEvent("m"), labels));

        pipeline.Dispatch(new QuickJumpKeyPressed(new KeyEvent("q"), labels));

        Assert.Equal([1], pipeline.Shell.Jumps);
        Assert.Equal(KeyMode.Normal, pipeline.Host.State.Shell.Mode);
    }

    [Fact]
    public async Task GivenABranchRow_WhenActivatingIt_ThenTheStashViewEndsAndTheGraphCursorMovesToItsCommit()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.Sidebar)));
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new SetSidebarFilterCommand("main")));
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new SetSidebarCursorCommand(1)));

        pipeline.Dispatch(new SidebarActionPressed(pipeline.TabId, ActionIds.SidebarActivate, null, [PaneKind.Sidebar], 10));

        await pipeline.Pump.RunUntilAsync(() => pipeline.Shell.SelectedGraphIndexes.Count > 0);
        Assert.Equal(1, pipeline.Shell.StashLeft);
        Assert.Equal([0], pipeline.Shell.SelectedGraphIndexes);
    }

    [Fact]
    public async Task GivenAStashRow_WhenActivatingIt_ThenTheShellIsAskedToShowThatStash()
    {
        using var repository = TestRepositories.WithStash();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.Stashes.Length > 0);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.Sidebar)));
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new SetSidebarFilterCommand("stash@")));
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new SetSidebarCursorCommand(1)));

        pipeline.Dispatch(new SidebarActionPressed(pipeline.TabId, ActionIds.SidebarActivate, null, [PaneKind.Sidebar], 10));

        Assert.Equal("stash@{0}", Assert.Single(pipeline.Shell.ShownStashes).Label);
        Assert.Empty(pipeline.Shell.SelectedGraphIndexes);
    }

    [Fact]
    public async Task GivenTheCursorMoves_WhenPressingAListMotionInTheSidebar_ThenTheShellMayFollowAShownStash()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.Sidebar)));

        pipeline.Dispatch(new SidebarActionPressed(pipeline.TabId, ActionIds.ListDown, null, [PaneKind.Sidebar], 10));

        Assert.Equal(1, pipeline.Shell.StashFollowed);
        Assert.Equal(1, pipeline.Tab.Sidebar.Cursor);
    }

    [Fact]
    public void GivenEveryConcreteEffect_WhenRunning_ThenTheRunnerHasACaseForIt()
    {
        var effects = ConcreteSubtypes.InstancesOf<AppEffect>();
        Assert.Contains(effects, effect => effect is TagEffect);
        Assert.Contains(effects, effect => effect is PostStatusEffect);
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var host = new Dispatcher(AppState.Empty, pump);
        using var runner = new EffectRunner<TabSession>(
            new FakeGitServices().Services,
            host,
            pump,
            new TabSessionFactory(),
            new EffectRunnerOptions<TabSession>
            {
                Statuses = new StatusReporter(host),
                Watcher = TestGit.QuietWatcherOptions,
            });

        foreach (var effect in effects)
        {
            // An empty effect may fail inside its handler (that becomes a failure command); only a missing case is a bug.
            Assert.False(MissesItsCase(runner, effect), $"{effect.GetType().Name} has no case in EffectRunner");
        }
    }

    [Fact]
    public async Task GivenAConflictedFile_WhenSavedThroughTheRunner_ThenTheFileIsWrittenAndHandedToStaging()
    {
        using var repository = TestRepositories.Conflicted();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Tab.WorkingTree is not null);
        var viewport = new ConflictViewport(10, 6);
        var viewports = new ConflictViewports(viewport, viewport, viewport, viewport);

        pipeline.Dispatch(new ConflictOpenRequested(true, viewports));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Conflict.IsOpen);
        pipeline.Dispatch(new ConflictActionPressed(ActionIds.ConflictEditorTheirs, null, viewports));
        pipeline.Dispatch(new ConflictActionPressed(ActionIds.ConflictEditorSave, null, viewports));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Conflict.IsOpen);

        var staged = Assert.Single(pipeline.Shell.StagedConflicts);
        Assert.Equal("conflict.txt", staged.Path);
        Assert.Equal(pipeline.TabId, staged.TabId);
        Assert.DoesNotContain(
            "<<<<<<<",
            File.ReadAllText(Path.Combine(repository.WorkingTreePath, "conflict.txt")),
            StringComparison.Ordinal);
        Assert.False(pipeline.Tab.Running.IsRunning(RunningOperationKind.Conflicts));
    }

    [Fact]
    public async Task GivenTheReflogKey_WhenOpenedThroughTheRunner_ThenTheListOpensAndAnEntryActionReachesTheShell()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new ReflogOpenRequested(null));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Reflog.IsOpen);
        pipeline.Dispatch(new ReflogActionPressed(ActionIds.ResetBranch, null, 10));

        var list = pipeline.Reflog.List!;
        var entry = Assert.Single(pipeline.Shell.EntryActions);
        Assert.Equal(ReflogEntryKind.Reset, entry.Kind);
        Assert.Equal(list.Selected!.Hash, entry.Hash);
        Assert.Equal(pipeline.TabId, entry.TabId);
    }

    [Fact]
    public async Task GivenTheTabIsClosedWhileTheReflogIsRead_WhenTheResultArrives_ThenNoListOpensForTheClosedTab()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new ReflogOpenRequested(null));

        pipeline.Dispatch(new CloseTabCommand(pipeline.TabId));
        await pipeline.Pump.RunUntilAsync(() => !pipeline.Reflog.IsReading);

        Assert.False(pipeline.Reflog.IsOpen);
    }

    [Fact]
    public async Task GivenTheSettingsKey_WhenOpenedThroughTheRunner_ThenTheScreenOpensAndTheFileOptionsReachTheShell()
    {
        using var repository = TestRepositories.Linear();
        var config = Directory.CreateTempSubdirectory("gitunia-settings-pipeline-").FullName;
        try
        {
            var services = new SettingsServices(
                new ConfigWriter(config),
                new SettingsEnvironment(config, Directory.Exists),
                SettingsRegistry.All,
                DefaultKeymap.Create());
            using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository, services);

            pipeline.Dispatch(new SettingsActionPressed(ActionIds.SettingsOpen, null));
            await pipeline.Pump.RunUntilAsync(() => pipeline.Settings.IsOpen);

            Assert.Single(pipeline.Shell.AppliedOptions);
            Assert.Contains(ThemeChoice.Auto, pipeline.Shell.AppliedThemes);
            Assert.False(pipeline.Settings.IsBusy);
        }
        finally
        {
            Directory.Delete(config, recursive: true);
        }
    }

    [Fact]
    public async Task GivenARunnerWithoutAConfigFile_WhenOpeningTheSettings_ThenItFailsAtOnceAndNothingStaysBusy()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new SettingsActionPressed(ActionIds.SettingsOpen, null));

        Assert.False(pipeline.Settings.IsBusy);
        Assert.False(pipeline.Settings.IsOpen);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
    }

    [Fact]
    public async Task GivenAPushThatNeedsCredentials_WhenItFailsThroughTheRunner_ThenTheShellOffersTheTerminalRetry()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        var writer = new AuthFailingTagWriter(TestGit.Services.Tags, "git@host: Permission denied (publickey).");
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services with { Tags = writer }, repository);
        pipeline.Dispatch(new TagPushRequested(pipeline.TabId, new TagTarget(SidebarRowKind.Tag, "v1", repository.Head)));
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt is not null);

        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsTagRunning);

        Assert.Equal(["Tag push|push -- origin refs/tags/v1"], pipeline.Shell.Retries);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
    }

    [Fact]
    public async Task GivenAFullBackgroundQueue_WhenAUserEffectRuns_ThenItFinishesWithoutWaitingForTheQueue()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Jobs signal when they start: nothing posts to the pump while they block, so waiting on the pump would hang.
        var started = new SemaphoreSlim(0);

        var blocked = Enumerable.Range(0, BackgroundWork.DefaultLimit + 2)
            .Select(_ => pipeline.Background.RunAsync(() =>
            {
                started.Release();
                return release.Task;
            }, CancellationToken.None))
            .ToArray();

        for (var i = 0; i < BackgroundWork.DefaultLimit; i++)
        {
            await started.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        Assert.Equal(BackgroundWork.DefaultLimit, pipeline.Background.Running);
        Assert.Equal(2, pipeline.Background.Waiting);

        pipeline.Dispatch(new TagCreateRequested(pipeline.TabId, new TagTarget(null, null, repository.Head)));
        pipeline.Type("v1");
        pipeline.Dispatch(new PromptAcceptedCommand());
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsTagRunning);

        Assert.Equal(repository.Head, repository.Git("rev-parse", "v1").Trim());
        Assert.Equal(2, pipeline.Background.Waiting);
        release.SetResult();
        await Task.WhenAll(blocked).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }

    private static bool MissesItsCase(EffectRunner<TabSession> runner, AppEffect effect)
    {
        try
        {
            runner.Run(effect);
        }
        catch (UnhandledEffectException)
        {
            return true;
        }

        return false;
    }

    [Fact]
    public async Task GivenTheFilesTab_WhenKeysOpenAFileAndClimbToTheParent_ThenTheRunnerAsksTheShell()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        var file = new FileTreeRow(FileTreeRowKind.File, "src/a.cs", "a.cs", 1, false, null, 0);

        pipeline.Dispatch(new FileTreeOpened(pipeline.TabId, new FileTreeCursor(file, 2)));
        pipeline.Dispatch(new FileTreeCollapsed(pipeline.TabId, new FileTreeCursor(file, 2)));

        Assert.Equal(["src/a.cs"], pipeline.Shell.OpenedFiles);
        Assert.Equal([2], pipeline.Shell.CursorMoves);
    }

    [Fact]
    public async Task GivenARangeInTheGraph_WhenCopyingIt_ThenTheRunnerAsksTheShellToCopyTheRows()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new VisualToggled(1, true));
        pipeline.Dispatch(new VisualCursorMoved(2));

        pipeline.Dispatch(new VisualCopyRequested());

        var copy = Assert.Single(pipeline.Shell.RangeCopies);
        Assert.Equal(new RowRange(1, 2), copy.Range);
        Assert.Equal(PaneKind.Graph, copy.Pane);
    }

    [Fact]
    public async Task GivenAWideScreen_WhenAFileIsOpenedInTheDiff_ThenTheRunnerLoadsThatCommitInTheTabsStore()
    {
        using var repository = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        var store = pipeline.Session.DiffStore!;
        var target = new DiffTarget(pipeline.Session.Graph[0].Id, pipeline.Session.Graph[1].Id, false, false);
        pipeline.Dispatch(new ScreenResizedCommand(new ScreenExtent(100, 40), pipeline.Host.Clock.GetUtcNow()));

        pipeline.Dispatch(new DiffFileOpened(pipeline.TabId, new DiffOpenFacts(target, true, new DiffCursor(null, "file.txt"))));
        await pipeline.Pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(target.Commit, store.Commit);
        Assert.Equal("file.txt", pipeline.Tab.View.DiffPane.PendingFile);
        Assert.Equal(PaneKind.Diff, pipeline.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAPaneWithState_WhenTheTabReloads_ThenThePaneStartsOverWithTheNewStores()
    {
        using var repository = TestRepositories.Linear();
        using var other = TestRepositories.Linear();
        using var pipeline = await Pipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new SetDiffPaneCommand(new DiffPaneState(ShownPath: "a.txt"))));
        var location = await TestGit.Services.Discovery.DiscoverAsync(
            other.WorkingTreePath,
            TestContext.Current.CancellationToken);

        pipeline.Reload(location);

        Assert.Equal(DiffPaneState.Empty, pipeline.Tab.View.DiffPane);
    }

    private sealed class Pipeline : IDisposable
    {
        private readonly EffectRunner<TabSession> _runner;

        private Pipeline(
            Dispatcher host,
            EffectRunner<TabSession> runner,
            UiPump pump,
            RecordingShell shell,
            StatusReporter statuses)
        {
            Host = host;
            _runner = runner;
            Pump = pump;
            Shell = shell;
            Statuses = statuses;
        }

        public Dispatcher Host { get; }

        public UiPump Pump { get; }

        public RecordingShell Shell { get; }

        public StatusReporter Statuses { get; }

        public TabId TabId => Host.Workspace.ActiveTabId!.Value;

        public ReflogSession Reflog => Host.State.Shell.Reflog;

        public SettingsSession Settings => Host.State.Shell.Settings;

        public ConflictSession Conflict => Host.State.Shell.Conflict;

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public bool HasRefs => Host.Workspace.FindTab(TabId)!.Refs.Length > 0;

        public OpenPrompt? Prompt => Host.State.Shell.Prompt;

        public bool IsTagRunning => Host.Workspace.FindTab(TabId)!.Running.IsRunning(RunningOperationKind.Tags);

        public BackgroundWork Background => _runner.Background;

        public TabSession Session => _runner.SessionOf(TabId)!;

        /// <summary>Builds the tab's session again, as switching to another worktree does.</summary>
        public void Reload(RepositoryLocation location) => Host.Dispatch(new SwitchWorktreeCommand(TabId, location));

        public static async Task<Pipeline> OpenAsync(
            GitServices services,
            TestRepository repository,
            SettingsServices? settings = null)
        {
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var host = new Dispatcher(AppState.Empty, pump);
            var shell = new RecordingShell();
            var statuses = new StatusReporter(host);
            var runner = new EffectRunner<TabSession>(
                services,
                host,
                pump,
                new TabSessionFactory(),
                new EffectRunnerOptions<TabSession>
                {
                    Statuses = statuses,
                    Watcher = TestGit.QuietWatcherOptions,
                    Shell = shell,
                    Settings = settings,
                });
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            host.Dispatch(new OpenRepositoryCommand(location));
            var pipeline = new Pipeline(host, runner, pump, shell, statuses);
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

        public void Dispose() => _runner.Dispose();
    }
}
