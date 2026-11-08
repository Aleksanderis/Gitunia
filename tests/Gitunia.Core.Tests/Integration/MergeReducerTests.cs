using Gitunia.Core.Operations;
using Gitunia.Core.Integration;
using Gitunia.Git.Merge;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Integration.IntegrationFixtures;

namespace Gitunia.Core.Tests.Integration;

public class MergeReducerTests
{
    private static readonly MergeTarget Topic = new("refs/heads/topic", "topic");

    [Theory]
    [InlineData(MergeMode.Default)]
    [InlineData(MergeMode.NoFastForward)]
    [InlineData(MergeMode.Squash)]
    public void GivenATarget_WhenRequested_ThenTheMergeStartsIntoTheCurrentBranchWithTheMode(MergeMode mode)
    {
        var effects = IntegrationReducer.Reduce(new MergeRequested(Tab, mode, Topic), Context());

        var merge = Assert.IsType<MergeBranchEffect>(Assert.Single(effects));
        Assert.Equal("main", merge.Branch);
        Assert.Equal(Topic, merge.Target);
        Assert.Equal(mode, merge.Mode);
    }

    [Fact]
    public void GivenNoTarget_WhenRequested_ThenNothingRunsAndTheUserIsTold()
    {
        var effects = IntegrationReducer.Reduce(new MergeRequested(Tab, MergeMode.Default, null), Context());

        Assert.StartsWith("Select a commit", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnotherCallOfTheTab_WhenRequested_ThenItIsBusy()
    {
        var effects = IntegrationReducer.Reduce(
            new MergeRequested(Tab, MergeMode.Default, Topic),
            Context(busy: true));

        Assert.Equal("Busy: another operation of this tab is still running", Warning(effects));
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge, "merge")]
    [InlineData(RepositoryOperation.Rebase, "rebase")]
    [InlineData(RepositoryOperation.CherryPick, "cherry-pick")]
    [InlineData(RepositoryOperation.Revert, "revert")]
    public void GivenAnOperationInProgress_WhenRequested_ThenItIsRefusedByNameAndPointsAtContinueAndAbort(
        RepositoryOperation kind,
        string name)
    {
        var effects = IntegrationReducer.Reduce(
            new MergeRequested(Tab, MergeMode.Default, Topic),
            Context(operation: Info(kind)));

        var text = Warning(effects);
        Assert.Contains($"finish or abort the {name} first", text, StringComparison.Ordinal);
        Assert.Contains(OperationKeys.Continue, text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADetachedHead_WhenRequested_ThenItIsRefused()
    {
        var effects = IntegrationReducer.Reduce(
            new MergeRequested(Tab, MergeMode.Default, Topic),
            Context(new HeadState(HeadKind.Detached, null, Tip)));

        Assert.Contains("detached", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnbornBranch_WhenRequested_ThenItIsRefused()
    {
        var effects = IntegrationReducer.Reduce(
            new MergeRequested(Tab, MergeMode.Default, Topic),
            Context(new HeadState(HeadKind.Unborn, "main", null)));

        Assert.Contains("no commits yet", Warning(effects), StringComparison.Ordinal);
    }
}
