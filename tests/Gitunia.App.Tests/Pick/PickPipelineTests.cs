using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Running;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Pick;

// Cherry-pick and revert end to end: command -> reducer -> effect -> runner -> handler -> real git -> result.
public class PickPipelineTests
{
    [Fact]
    public async Task GivenAGraphCommit_WhenCherryPicking_ThenItLandsOnHeadTheStatusNamesTheUndoAndTheTabRefreshes()
    {
        using var repository = Diverged();
        var old = repository.Head;
        var topic = Hash(repository, "topic");
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new PickRequested(pipeline.TabId, PickKind.CherryPick, new PickTarget(null, [topic])));
        Assert.True(IsPickRunning(pipeline));
        await pipeline.UntilAsync(() => !pipeline.IsBusy && Tip(pipeline) != old);

        Assert.Equal("topic two", repository.Git("log", "-1", "--format=%s").Trim());
        var latest = pipeline.Statuses.Log.Latest!;
        Assert.StartsWith("Cherry-picked ", latest.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("git reset", latest.Text, StringComparison.Ordinal);
        Assert.Equal($"Undo: git reset --hard {old} (the reflog has it)", latest.Detail);
        Assert.Equal(repository.Head, Tip(pipeline));
    }

    [Fact]
    public async Task GivenARangeNewestFirst_WhenReverting_ThenBothCommitsAreUndone()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("b.txt", "2"))
            .Commit("three", new TestFile("c.txt", "3"));
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        var target = new PickTarget(null, [Hash(repository, "HEAD"), Hash(repository, "HEAD~1")]);

        pipeline.Dispatch(new PickRequested(pipeline.TabId, PickKind.Revert, target));
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "c.txt")));
        Assert.StartsWith("Reverted 2 commits onto main", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenReverting_ThenTheMainlineIsAskedAndTheAnswerRunsIt()
    {
        using var repository = MergedTopic();
        var merge = repository.Head;
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new PickRequested(pipeline.TabId, PickKind.Revert, new PickTarget(null, [merge])));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Mainline is not null);

        Assert.Equal(KeyMode.Insert, pipeline.Mode);
        Assert.False(pipeline.IsBusy);
        pipeline.Dispatch(new PickMainlineChosen(pipeline.TabId, 2));
        Assert.Null(pipeline.Host.State.Shell.Mainline);
        Assert.Equal(KeyMode.Normal, pipeline.Mode);
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "m.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.StartsWith("Reverted ", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheMainlineQuestion_WhenItIsCancelled_ThenNothingRunsAndTheModeReturns()
    {
        using var repository = MergedTopic();
        var merge = repository.Head;
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);
        pipeline.Dispatch(new PickRequested(pipeline.TabId, PickKind.Revert, new PickTarget(null, [merge])));
        await pipeline.UntilAsync(() => pipeline.Host.State.Shell.Mainline is not null);

        pipeline.Dispatch(new PickMainlineCancelled(pipeline.TabId));

        Assert.Null(pipeline.Host.State.Shell.Mainline);
        Assert.Equal(KeyMode.Normal, pipeline.Mode);
        Assert.False(pipeline.IsBusy);
        Assert.Equal(merge, repository.Head);
    }

    [Fact]
    public async Task GivenAConflictingCommit_WhenCherryPicking_ThenAWarningPointsToContinueAndTheOperationIsLoaded()
    {
        using var repository = TestRepositories.ConflictingBranches();
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new PickRequested(
            pipeline.TabId,
            PickKind.CherryPick,
            new PickTarget(SidebarRowKind.LocalBranch, [Hash(repository, "other")])));
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        var latest = pipeline.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Contains("<space>gc", latest.Text, StringComparison.Ordinal);
        var location = pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Location;
        Assert.Equal(RepositoryOperation.CherryPick, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenADirtyTreeGitCannotPickOver_WhenCherryPicking_ThenGitsMessageIsShownAsAnError()
    {
        using var repository = Diverged().WriteFile("b.txt", "dirty");
        using var pipeline = await FeaturePipeline.OpenAsync(TestGit.Services, repository);

        pipeline.Dispatch(new PickRequested(
            pipeline.TabId,
            PickKind.CherryPick,
            new PickTarget(null, [Hash(repository, "topic")])));
        await pipeline.UntilAsync(() => !pipeline.IsBusy && pipeline.Statuses.Log.Latest is not null);

        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest!.Severity);
        Assert.StartsWith("Could not cherry-pick ", pipeline.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    private static bool IsPickRunning(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Running.IsRunning(RunningOperationKind.Pick);

    private static string? Tip(FeaturePipeline pipeline) =>
        pipeline.Host.Workspace.FindTab(pipeline.TabId)!.Head?.CommitHash;

    // Main has "main two" (m.txt), topic has "topic two" (b.txt), both forked from "one".
    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"));

    // Main gets m.txt and merges topic (b.txt), so HEAD is a merge commit.
    private static TestRepository MergedTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"))
            .Merge("topic");

    private static string Hash(TestRepository repository, string revision) =>
        repository.Git("rev-parse", revision).Trim();
}
