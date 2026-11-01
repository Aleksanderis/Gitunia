using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Staging;

// Staging end to end: command -> reducer -> effect -> runner -> handler -> real git -> status read -> result command.
public class StagingPipelineTests
{
    [Fact]
    public async Task GivenAModifiedFile_WhenStagingIt_ThenGitHasItAndTheTabIsFreeOnlyOnceTheListsShowIt()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        using var pipeline = await OpenWithStatusAsync(repository);

        pipeline.Dispatch(new StagingRequested(pipeline.TabId, StagingAction.Stage));
        Assert.True(IsStaging(pipeline));
        await pipeline.UntilAsync(() => !IsStaging(pipeline));

        // The result is reduced after the status was read again: the lists already show the change.
        var tree = pipeline.Host.Workspace.FindTab(pipeline.TabId)!.WorkingTree!;
        Assert.Equal(["file.txt"], tree.Staged.Select(entry => entry.Path));
        Assert.Empty(tree.Unstaged);
        Assert.Equal("file.txt", repository.Git("diff", "--cached", "--name-only").Trim());
        Assert.Equal("Staged file.txt", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAStagedFile_WhenUnstagingIt_ThenItIsBackInTheWorkingTree()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        repository.Git("add", "file.txt");
        using var pipeline = await OpenWithStatusAsync(repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.WorkingStaged)));

        pipeline.Dispatch(new StagingRequested(pipeline.TabId, StagingAction.Unstage));
        await pipeline.UntilAsync(() => !IsStaging(pipeline));

        Assert.Empty(repository.Git("diff", "--cached", "--name-only").Trim());
        Assert.Equal("Unstaged file.txt", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenACommitWaitsForStageAll_WhenTheStageWorks_ThenItsActionRunsAfterTheListsShowTheFiles()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        using var pipeline = await OpenWithStatusAsync(repository);
        var stagedWhenRun = -1;
        var host = pipeline.Host;
        var tabId = pipeline.TabId;
        var then = new RunActionCommand(() =>
            stagedWhenRun = host.Workspace.FindTab(tabId)!.WorkingTree!.Staged.Count);

        pipeline.Dispatch(new StageAllThenRequested(pipeline.TabId, then));
        await pipeline.UntilAsync(() => stagedWhenRun >= 0);

        Assert.Equal(1, stagedWhenRun);
        Assert.False(IsStaging(pipeline));
    }

    [Fact]
    public async Task GivenGitFails_WhenStaging_ThenTheErrorIsShownAndTheCommandThatWaitedNeverRuns()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        using var pipeline = await OpenWithStatusAsync(repository);
        await File.WriteAllTextAsync(
            Path.Combine(repository.WorkingTreePath, ".git", "index.lock"),
            string.Empty,
            TestContext.Current.CancellationToken);
        var ran = false;

        pipeline.Dispatch(new StageAllThenRequested(pipeline.TabId, new RunActionCommand(() => ran = true)));
        await pipeline.UntilAsync(() => !IsStaging(pipeline));

        Assert.False(ran);
        Assert.StartsWith("Could not stage: ", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAStagedHunk_WhenTheResultCarriesACursorMove_ThenTheTerminalLayerIsAskedToMoveTheDiffCursor()
    {
        using var repository = TestRepositories.Linear();
        var shell = new RecordingShell();
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository, shell);
        var move = new DiffCursorMove(1, -1);

        pipeline.Dispatch(
            new StagingSucceeded(pipeline.TabId, "Staged change", StatusSeverity.Info, new StagingFollow(Move: move)));

        Assert.Equal(new MoveDiffCursorEffect(pipeline.TabId, move), Assert.Single(shell.DiffCursorMoves));
    }

    private static bool IsStaging(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Running.IsRunning(RunningOperationKind.Staging);

    private static async Task<FeaturePipeline> OpenWithStatusAsync(TestRepository repository)
    {
        var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new OpenWorkingTreeCommand()));
        await pipeline.UntilAsync(() => pipeline.Host.Workspace.FindTab(pipeline.TabId)!.WorkingTree is not null);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.WorkingUnstaged)));

        return pipeline;
    }
}
