using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Integration.IntegrationFixtures;

namespace Gitunia.Core.Tests.Integration;

public class OperationReducerTests
{
    private const string Commit = "3333333333333333333333333333333333333333";

    [Theory]
    [InlineData(OperationAction.Continue)]
    [InlineData(OperationAction.Abort)]
    [InlineData(OperationAction.Skip)]
    public void GivenNoOperation_WhenRequested_ThenItIsRefusedWithoutGit(OperationAction action)
    {
        var effects = IntegrationReducer.Reduce(new OperationRequested(Tab, action), Context());

        Assert.Equal("No merge, rebase, cherry-pick or revert is in progress", Warning(effects));
    }

    [Theory]
    [InlineData(1, "1 file still conflicted")]
    [InlineData(3, "3 files still conflicted")]
    public void GivenConflictedFiles_WhenContinuing_ThenItIsRefusedAndCountsThem(int conflicts, string expected)
    {
        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Continue),
            Context(operation: Info(RepositoryOperation.Rebase), conflicts: conflicts));

        Assert.StartsWith(expected, Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAResolvedMerge_WhenContinuing_ThenTheCallStartsOnTheOperationAndBranchTheUserSaw()
    {
        var info = Info(RepositoryOperation.Merge);

        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Continue),
            Context(operation: info));

        var proceed = Assert.IsType<ContinueOperationEffect>(Assert.Single(effects));
        Assert.Equal(info, proceed.Info);
        Assert.Equal("main", proceed.Branch);
    }

    [Fact]
    public void GivenAnotherCallOfTheTab_WhenContinuing_ThenItIsBusy()
    {
        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Continue),
            Context(operation: Info(RepositoryOperation.Merge), busy: true));

        Assert.Equal("Busy: another operation of this tab is still running", Warning(effects));
    }

    [Fact]
    public void GivenARebase_WhenAborting_ThenTheDialogNamesTheOldTipAndNothingRunsYet()
    {
        var info = Info(RepositoryOperation.Rebase, original: "4444444444444444444444444444444444444444");

        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Abort),
            Context(operation: info));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Abort rebase", confirm.Prompt.Title);
        Assert.Contains("main goes back to 4444444", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("reflog", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(new OperationAbortConfirmed(Tab, info, "main"), confirm.OnAccepted);
        Assert.Equal("The screen is too small to ask: nothing was changed", confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenAMerge_WhenAborting_ThenTheDialogSaysTheBranchHasNotMoved()
    {
        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Abort),
            Context(operation: Info(RepositoryOperation.Merge)));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Contains("the branch itself has not moved", confirm.Prompt.Consequence, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMerge_WhenSkipping_ThenItIsRefusedWithoutAsking()
    {
        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Skip),
            Context(operation: Info(RepositoryOperation.Merge)));

        Assert.Equal("A merge cannot be skipped: continue or abort it", Warning(effects));
    }

    [Fact]
    public void GivenARebaseStoppedOnACommit_WhenSkipping_ThenTheDialogNamesThatCommit()
    {
        var info = Info(RepositoryOperation.Rebase, Commit);

        var effects = IntegrationReducer.Reduce(
            new OperationRequested(Tab, OperationAction.Skip),
            Context(operation: info));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Contains("Skip 3333333 a subject?", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Equal(new OperationSkipConfirmed(Tab, info), confirm.OnAccepted);
    }

    [Fact]
    public void GivenTheAbortDialogAnswered_WhenNothingElseRuns_ThenTheAbortStartsOnWhatTheDialogNamed()
    {
        var info = Info(RepositoryOperation.Rebase);

        var effects = IntegrationReducer.Reduce(new OperationAbortConfirmed(Tab, info, "main"), Context());

        var abort = Assert.IsType<AbortOperationEffect>(Assert.Single(effects));
        Assert.Equal(info, abort.Info);
        Assert.Equal("main", abort.Branch);
    }

    [Fact]
    public void GivenAnotherCallStartsWhileAsking_WhenConfirmingTheAbort_ThenNothingIsAborted()
    {
        var effects = IntegrationReducer.Reduce(
            new OperationAbortConfirmed(Tab, Info(RepositoryOperation.Rebase), "main"),
            Context(busy: true));

        Assert.EndsWith("nothing was aborted", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheSkipDialogAnswered_WhenTheOperationIsTheSame_ThenTheSkipStarts()
    {
        var info = Info(RepositoryOperation.Rebase, Commit);

        var effects = IntegrationReducer.Reduce(
            new OperationSkipConfirmed(Tab, info),
            Context(operation: info));

        Assert.Equal(info, Assert.IsType<SkipOperationEffect>(Assert.Single(effects)).Info);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenTheOperationMovedOnWhileAsking_WhenConfirmingTheSkip_ThenNothingIsSkipped(bool otherCommit)
    {
        var asked = Info(RepositoryOperation.Rebase, Commit);
        var now = otherCommit ? Info(RepositoryOperation.Rebase, "5555555555555555555555555555555555555555") : null;

        var effects = IntegrationReducer.Reduce(new OperationSkipConfirmed(Tab, asked), Context(operation: now));

        Assert.Equal("The operation moved on while asking: nothing was skipped", Warning(effects));
    }

    [Fact]
    public void GivenAnotherCallStartsWhileAsking_WhenConfirmingTheSkip_ThenNothingIsSkipped()
    {
        var info = Info(RepositoryOperation.Rebase, Commit);

        var effects = IntegrationReducer.Reduce(
            new OperationSkipConfirmed(Tab, info),
            Context(operation: info, busy: true));

        Assert.EndsWith("nothing was skipped", Warning(effects), StringComparison.Ordinal);
    }
}
