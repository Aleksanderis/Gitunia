using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Discard;
using Gitunia.Core.Overlays;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Discard;

// Discard end to end: command, reducer, dialog, answer, effect, runner, handler, real repository, status read, result.
public class DiscardPipelineTests
{
    [Fact]
    public async Task GivenAModifiedFile_WhenDiscardingAndAnsweringYes_ThenTheFileIsRestoredAndTheTabIsFreeOnlyOnceTheListsShowIt()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        using var pipeline = await OpenWithStatusAsync(repository);

        pipeline.Dispatch(new DiscardRequested(pipeline.TabId));
        Assert.NotNull(pipeline.Confirm);
        Assert.False(IsDiscarding(pipeline));
        pipeline.Answer(ConfirmAnswer.Yes);
        Assert.True(IsDiscarding(pipeline));
        await pipeline.UntilAsync(() => !IsDiscarding(pipeline));

        var tree = pipeline.Host.Workspace.FindTab(pipeline.TabId)!.WorkingTree!;
        Assert.Empty(tree.Unstaged);
        Assert.Empty(repository.Git("status", "--porcelain").Trim());
        Assert.Equal("Discarded all changes to file.txt", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAnUntrackedFile_WhenDiscardingAndAnsweringNo_ThenTheFileStays()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("new.txt", "new\n");
        using var pipeline = await OpenWithStatusAsync(repository);

        pipeline.Dispatch(new DiscardRequested(pipeline.TabId));
        pipeline.Answer(ConfirmAnswer.No);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(IsDiscarding(pipeline));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
    }

    [Fact]
    public async Task GivenTheRepositoryIsLocked_WhenDiscarding_ThenTheErrorIsShownAndTheTabIsFree()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        using var pipeline = await OpenWithStatusAsync(repository);
        await File.WriteAllTextAsync(
            Path.Combine(repository.WorkingTreePath, ".git", "index.lock"),
            string.Empty,
            TestContext.Current.CancellationToken);

        pipeline.Dispatch(new DiscardRequested(pipeline.TabId));
        pipeline.Answer(ConfirmAnswer.Yes);
        await pipeline.UntilAsync(() => !IsDiscarding(pipeline));

        Assert.StartsWith("Could not discard: ", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    private static bool IsDiscarding(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Running.IsRunning(RunningOperationKind.Discard);

    private static async Task<FeaturePipeline> OpenWithStatusAsync(TestRepository repository)
    {
        var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new OpenWorkingTreeCommand()));
        await pipeline.UntilAsync(() => pipeline.Host.Workspace.FindTab(pipeline.TabId)!.WorkingTree is not null);
        pipeline.Dispatch(new TabCommand(pipeline.TabId, new FocusPaneCommand(PaneKind.WorkingUnstaged)));

        return pipeline;
    }
}
