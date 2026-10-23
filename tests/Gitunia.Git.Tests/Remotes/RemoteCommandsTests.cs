using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public class RemoteCommandsTests
{
    [Fact]
    public void GivenAFetchWithPrune_WhenBuilt_ThenProgressIsOnlyAddedForARedirectedRun()
    {
        var request = new FetchRequest(null, false, true);

        Assert.Equal(["fetch", "--progress", "--prune"], RemoteCommands.BuildFetch(request, true));
        Assert.Equal(["fetch", "--prune"], RemoteCommands.BuildFetch(request, false));
    }

    [Fact]
    public void GivenFetchAll_WhenBuilt_ThenItNamesNoRemote()
    {
        Assert.Equal(
            ["fetch", "--prune", "--all"],
            RemoteCommands.BuildFetch(new FetchRequest("origin", true, true), false));
    }

    [Fact]
    public void GivenANamedRemote_WhenBuilt_ThenItFollowsTheEndOfOptions()
    {
        Assert.Equal(
            ["fetch", "--", "origin"],
            RemoteCommands.BuildFetch(new FetchRequest("origin", false, false), false));
    }

    [Fact]
    public void GivenAPullForTheTerminal_WhenBuilt_ThenItIsFastForwardOnlyAndNeverARebase()
    {
        Assert.Equal(["pull", "--no-rebase", "--ff-only"], RemoteCommands.BuildPull());
    }

    [Fact]
    public void GivenARebasingPullConfig_WhenRunningThePullForTheTerminal_ThenADivergedBranchIsNotRebased()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 1);
        repository.Git("config", "pull.rebase", "true");

        var before = repository.Head;
        var arguments = RemoteCommands.BuildPull().ToArray();

        Assert.NotNull(RunGitRecordingFailure(repository, arguments));

        Assert.Equal(before, repository.Head);
    }

    [Fact]
    public void GivenABranchBehindItsUpstream_WhenRunningThePullForTheTerminal_ThenItFastForwards()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 2);

        repository.Git([.. RemoteCommands.BuildPull()]);

        Assert.Equal(repository.Git("rev-parse", "@{upstream}").Trim(), repository.Head);
    }

    [Fact]
    public void GivenAForcePushWithALease_WhenBuilt_ThenOnlyTheLeaseForcesAndItTargetsTheRemoteBranch()
    {
        var request = new PushRequest("origin", "topic", false, new PushLease("topic", "abc123"));

        Assert.Equal(
            ["push", "--force-with-lease=refs/heads/topic:abc123", "--", "origin", "topic:refs/heads/topic"],
            RemoteCommands.BuildPush(request, false));
    }

    [Fact]
    public void GivenAFirstPush_WhenBuilt_ThenItSetsTheUpstream()
    {
        Assert.Equal(
            ["push", "--progress", "--set-upstream", "--", "origin", "topic"],
            RemoteCommands.BuildPush(new PushRequest("origin", "topic", true), true));
    }

    [Fact]
    public void GivenARemoteBranchDelete_WhenBuilt_ThenTheTerminalRetryIsTheBackgroundCommandWithoutQuietFlags()
    {
        Assert.Equal(
            [
                "push", "--no-progress", "--delete", "--force-with-lease=refs/heads/topic:abc", "--", "origin",
                "refs/heads/topic",
            ],
            RemoteCommands.BuildDeleteBranch("origin", "topic", "abc", true));
        Assert.Equal(
            ["push", "--delete", "--force-with-lease=refs/heads/topic:abc", "--", "origin", "refs/heads/topic"],
            RemoteCommands.BuildDeleteBranch("origin", "topic", "abc", false));
    }

    [Fact]
    public void GivenATagPushAndDelete_WhenBuilt_ThenOnlyThatTagRefIsNamed()
    {
        Assert.Equal(
            ["push", "--no-progress", "--", "origin", "refs/tags/v1"],
            RemoteCommands.BuildTagPush("origin", "v1", true));
        Assert.Equal(["push", "--", "origin", "refs/tags/v1"], RemoteCommands.BuildTagPush("origin", "v1", false));
        Assert.Equal(
            ["push", "--delete", "--", "origin", "refs/tags/v1"],
            RemoteCommands.BuildTagDelete("origin", "v1", false));
    }

    private static Exception? RunGitRecordingFailure(TestRepository repository, string[] arguments)
    {
        return Record.Exception(() => repository.Git(arguments));
    }
}
