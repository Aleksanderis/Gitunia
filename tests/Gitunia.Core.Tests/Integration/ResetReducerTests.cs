using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Core.Tests.Integration.IntegrationFixtures;

namespace Gitunia.Core.Tests.Integration;

public class ResetReducerTests
{
    private const string Target = "2222222222222222222222222222222222222222";

    [Fact]
    public void GivenACommit_WhenRequested_ThenTheOneKeyModePromptOpensForTheTab()
    {
        var effects = IntegrationReducer.Reduce(new ResetRequested(Tab, Target), Context());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Reset", request.Title);
        Assert.Equal("Move main to 2222222", request.Context);
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(new ResetModeAnswer(Tab, Target, "main"), request.OnAccepted);
        Assert.Equal(["s", "m", "h"], request.KeyChoices!.Value.Select(choice => choice.Key));
    }

    [Theory]
    [InlineData(ResetModeAnswer.Soft, ResetMode.Soft)]
    [InlineData(ResetModeAnswer.Mixed, ResetMode.Mixed)]
    [InlineData(ResetModeAnswer.Hard, ResetMode.Hard)]
    public void GivenAModeKey_WhenTheAnswerIsReduced_ThenTheTargetCarriesTheModeAndWhatTheUserSaw(
        string choice,
        ResetMode mode)
    {
        var answered = new ResetModeAnswer(Tab, Target, "main").Answered(new PromptAnswer(string.Empty, choice));

        Assert.Equal(new ResetModeChosen(Tab, new ResetTarget(Target, "main", mode)), answered);
    }

    [Fact]
    public void GivenNoCommitSelected_WhenRequested_ThenItWarnsWithoutAPrompt()
    {
        var effects = IntegrationReducer.Reduce(new ResetRequested(Tab, null), Context());

        Assert.StartsWith("Select a commit", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAResetRuns_WhenRequested_ThenItIsBusy()
    {
        var effects = IntegrationReducer.Reduce(new ResetRequested(Tab, Target), Context(busy: true));

        Assert.Equal("Busy: the last reset is still running", Warning(effects));
    }

    [Fact]
    public void GivenADetachedHead_WhenRequested_ThenItIsRefusedWithoutAPrompt()
    {
        var head = new HeadState(HeadKind.Detached, null, Tip);

        var effects = IntegrationReducer.Reduce(new ResetRequested(Tab, Target), Context(head));

        Assert.Equal("HEAD is detached: switch to a branch first", Warning(effects));
    }

    [Fact]
    public void GivenAnUnbornBranch_WhenRequested_ThenItIsRefused()
    {
        var head = new HeadState(HeadKind.Unborn, "main", null);

        var effects = IntegrationReducer.Reduce(new ResetRequested(Tab, Target), Context(head));

        Assert.Equal("The branch has no commits yet: nothing to reset", Warning(effects));
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge, "merge")]
    [InlineData(RepositoryOperation.Rebase, "rebase")]
    [InlineData(RepositoryOperation.CherryPick, "cherry-pick")]
    [InlineData(RepositoryOperation.Revert, "revert")]
    public void GivenAnOperationInProgress_WhenRequested_ThenItIsRefusedByName(RepositoryOperation kind, string name)
    {
        var effects = IntegrationReducer.Reduce(
            new ResetRequested(Tab, Target),
            Context(operation: Info(kind)));

        Assert.Equal($"A {name} is in progress: finish or abort the {name} first", Warning(effects));
    }

    [Fact]
    public void GivenAModeKey_WhenChosen_ThenTheStatusIsReadBeforeAnythingMoves()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Hard);

        var effects = IntegrationReducer.Reduce(new ResetModeChosen(Tab, target), Context());

        Assert.Equal(target, Assert.IsType<PrepareResetEffect>(Assert.Single(effects)).Target);
    }

    [Fact]
    public void GivenAResetRunsMeanwhile_WhenAModeIsChosen_ThenItIsBusy()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Soft);

        var effects = IntegrationReducer.Reduce(new ResetModeChosen(Tab, target), Context(busy: true));

        Assert.Equal("Busy: the last reset is still running", Warning(effects));
    }

    [Fact]
    public void GivenSoft_WhenPrepared_ThenTheResetRunsAtOnce()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Soft);

        var effects = IntegrationReducer.Reduce(Prepared(target, RepositoryOperation.None), Context());

        Assert.Equal(target, Assert.IsType<ResetBranchEffect>(Assert.Single(effects)).Target);
    }

    [Fact]
    public void GivenMixedAndNothingStaged_WhenPrepared_ThenTheResetRunsAtOnce()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Mixed);
        var unstaged = Entry("a.txt", staged: false);

        var effects = IntegrationReducer.Reduce(
            Prepared(target, RepositoryOperation.None, unstaged),
            Context());

        Assert.IsType<ResetBranchEffect>(Assert.Single(effects));
    }

    [Fact]
    public void GivenMixedAndPartlyStagedFiles_WhenPrepared_ThenAConfirmNamesThemAndTheUndo()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Mixed);
        var partial = Entry("a.txt", staged: true, unstaged: true);

        var effects = IntegrationReducer.Reduce(
            Prepared(target, RepositoryOperation.None, partial),
            Context());

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Mixed reset", confirm.Prompt.Title);
        Assert.Contains(
            "1 file loses the staged state (1 partially staged)",
            string.Join(' ', confirm.Prompt.Lines),
            StringComparison.Ordinal);
        Assert.Contains("cannot be recovered from the reflog", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains($"git reset --mixed {Tip}", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(new ResetConfirmed(Tab, target), confirm.OnAccepted);
        Assert.Equal("The screen is too small to ask: nothing was reset", confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenHardAndADirtyFile_WhenPrepared_ThenTheConfirmListsItAndTheUndoAndWhatIsLost()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Hard);

        var effects = IntegrationReducer.Reduce(
            Prepared(target, RepositoryOperation.None, Entry("a.txt", staged: false)),
            Context());

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        var lines = string.Join('\n', confirm.Prompt.Lines);
        Assert.Equal("Hard reset", confirm.Prompt.Title);
        Assert.Equal("Reset hard", confirm.Prompt.YesLabel);
        Assert.Contains("to 2222222 with --hard", lines, StringComparison.Ordinal);
        Assert.Contains("1 file with uncommitted changes will be overwritten", lines, StringComparison.Ordinal);
        Assert.Contains("  a.txt", lines, StringComparison.Ordinal);
        Assert.Contains("Untracked files stay unless", lines, StringComparison.Ordinal);
        Assert.Contains($"git reset --hard {Tip}", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("ORIG_HEAD", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Contains("cannot be restored", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(new ResetConfirmed(Tab, target), confirm.OnAccepted);
    }

    [Fact]
    public void GivenHardAndManyFiles_WhenPrepared_ThenTheConfirmCountsAllAndListsOnlyTheFirstSix()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Hard);
        var entries = Enumerable.Range(1, 9).Select(index => Entry($"f{index}.txt", staged: false)).ToArray();

        var effects = IntegrationReducer.Reduce(Prepared(target, RepositoryOperation.None, entries), Context());

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Contains(lines, line => line.StartsWith("9 files with uncommitted changes", StringComparison.Ordinal));
        Assert.Contains("  f6.txt", lines);
        Assert.DoesNotContain("  f7.txt", lines);
        Assert.Contains("  and 3 more", lines);
    }

    [Fact]
    public void GivenHardAndACleanTree_WhenPrepared_ThenTheConfirmSaysThereIsNothingToLose()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Hard);

        var effects = IntegrationReducer.Reduce(Prepared(target, RepositoryOperation.None), Context());

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Contains("There are no uncommitted changes to lose.", lines);
    }

    [Fact]
    public void GivenAnOperationShownByTheFilesWhilePreparing_WhenPrepared_ThenItIsRefusedAndNothingMoves()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Soft);

        var effects = IntegrationReducer.Reduce(Prepared(target, RepositoryOperation.ApplyMailbox), Context());

        Assert.Equal("A mailbox apply is in progress: finish or abort the mailbox apply first", Warning(effects));
    }

    [Fact]
    public void GivenTheHeadMovedToDetachedWhilePreparing_WhenPrepared_ThenItIsRefused()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Soft);
        var head = new HeadState(HeadKind.Detached, null, Tip);

        var effects = IntegrationReducer.Reduce(Prepared(target, RepositoryOperation.None), Context(head));

        Assert.Contains("HEAD is detached", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConfirmedReset_WhenNothingElseRuns_ThenTheResetStartsOnTheDialogsTarget()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Hard);

        var effects = IntegrationReducer.Reduce(new ResetConfirmed(Tab, target), Context());

        Assert.Equal(target, Assert.IsType<ResetBranchEffect>(Assert.Single(effects)).Target);
    }

    [Fact]
    public void GivenAnotherCallStartedWhileAsking_WhenConfirmed_ThenNothingIsReset()
    {
        var target = new ResetTarget(Target, "main", ResetMode.Hard);

        var effects = IntegrationReducer.Reduce(new ResetConfirmed(Tab, target), Context(busy: true));

        Assert.Equal("Busy: the last reset is still running; nothing was reset", Warning(effects));
    }

    [Fact]
    public void GivenAFinishedReset_WhenTheResultArrives_ThenTheStatusIsPostedAndTheTabRefreshed()
    {
        var effects = IntegrationReducer.Reduce(
            new IntegrationSucceeded(Tab, "Reset main", StatusSeverity.Info),
            Context());

        Assert.Equal("Reset main", Assert.IsType<PostStatusEffect>(effects[0]).Text);
        Assert.Equal(new RefreshTabEffect(Tab), effects[1]);
    }

    [Fact]
    public void GivenAFailedStatusRead_WhenTheResultArrives_ThenTheErrorShowsAndTheTabIsNotRefreshed()
    {
        var effects = IntegrationReducer.Reduce(new IntegrationFailed(Tab, "boom", false), Context());

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
    }

    [Fact]
    public void GivenACancelledReset_WhenTheResultArrives_ThenNothingIsPostedOrRefreshed()
    {
        Assert.Empty(IntegrationReducer.Reduce(new IntegrationCancelled(Tab), Context()));
    }

    private static ResetPrepared Prepared(
        ResetTarget target,
        RepositoryOperation operation,
        params StatusEntry[] changes) =>
        new(Tab, target, operation, [.. changes]);

    private static StatusEntry Entry(string path, bool staged, bool unstaged = true) =>
        new(
            StatusEntryKind.Ordinary,
            path,
            null,
            staged ? FileChangeKind.Modified : FileChangeKind.Unmodified,
            unstaged ? FileChangeKind.Modified : FileChangeKind.Unmodified,
            null,
            ConflictKind.None,
            null);
}
