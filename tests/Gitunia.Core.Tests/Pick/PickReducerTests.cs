using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Running;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Git.Pick;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Pick.PickTestData;

namespace Gitunia.Core.Tests.Pick;

public class PickReducerTests
{
    [Fact]
    public void GivenARangeNewestFirst_WhenCherryPicking_ThenTheCommitsAreReadOldestFirst()
    {
        var update = PickReducer.Reduce(Request(PickKind.CherryPick, null, First, Second), Context());

        var read = Assert.IsType<ReadPickCommitsEffect>(Assert.Single(update.Effects));
        Assert.Equal([Second, First], read.Hashes);
        Assert.Equal(PickKind.CherryPick, read.PickKind);
        Assert.Equal(Tab, read.TabId);
        Assert.Null(update.Question);
    }

    [Fact]
    public void GivenARangeNewestFirst_WhenReverting_ThenTheCommitsKeepTheGraphOrder()
    {
        var update = PickReducer.Reduce(Request(PickKind.Revert, null, First, Second), Context());

        Assert.Equal([First, Second], Assert.IsType<ReadPickCommitsEffect>(Assert.Single(update.Effects)).Hashes);
    }

    [Theory]
    [InlineData(SidebarRowKind.LocalBranch)]
    [InlineData(SidebarRowKind.RemoteBranch)]
    [InlineData(SidebarRowKind.Tag)]
    public void GivenARefRow_WhenCherryPicking_ThenItsTipIsRead(SidebarRowKind kind)
    {
        var update = PickReducer.Reduce(Request(PickKind.CherryPick, kind, Second), Context());

        Assert.Equal([Second], Assert.IsType<ReadPickCommitsEffect>(Assert.Single(update.Effects)).Hashes);
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public void GivenASidebarRowThatIsNotARef_WhenPicking_ThenNothingRuns(SidebarRowKind kind)
    {
        var update = PickReducer.Reduce(Request(PickKind.CherryPick, kind, Second), Context());

        Assert.Equal(PickReducer.SelectNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenNothingSelected_WhenPicking_ThenItAsksForASelection()
    {
        var update = PickReducer.Reduce(Request(PickKind.Revert, null), Context());

        Assert.Equal(PickReducer.SelectNotice, Warning(update.Effects));
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge, "merge")]
    [InlineData(RepositoryOperation.Rebase, "rebase")]
    [InlineData(RepositoryOperation.CherryPick, "cherry-pick")]
    [InlineData(RepositoryOperation.Revert, "revert")]
    public void GivenAnOperationInProgress_WhenPicking_ThenItIsRefusedByNameAndNothingRuns(
        RepositoryOperation kind,
        string name)
    {
        var context = Context(operation: Stopped(kind));

        var cherry = PickReducer.Reduce(Request(PickKind.CherryPick, null, First), context);
        var revert = PickReducer.Reduce(Request(PickKind.Revert, null, First), context);

        Assert.Contains($"finish or abort the {name} first", Warning(cherry.Effects), StringComparison.Ordinal);
        Assert.Contains("<space>gc", Warning(revert.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnbornBranch_WhenPicking_ThenItIsRefused()
    {
        var context = Context(head: new HeadState(HeadKind.Unborn, "main", null));

        var update = PickReducer.Reduce(Request(PickKind.CherryPick, null, First), context);

        Assert.Contains("no commits yet", Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARunningPick_WhenPickingAgain_ThenItIsBusyAndNothingStarts()
    {
        var update = PickReducer.Reduce(Request(PickKind.CherryPick, null, First), Context(busy: true));

        Assert.Equal(PickReducer.BusyNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenPlainCommits_WhenTheyAreRead_ThenThePickRunsAtOnceOntoTheBranch()
    {
        var read = new PickCommitsRead(Tab, PickKind.CherryPick, [Plain(First)]);

        var update = PickReducer.Reduce(read, Context(busy: true));

        var run = Assert.IsType<RunPickEffect>(Assert.Single(update.Effects));
        Assert.Null(run.Mainline);
        Assert.Equal("main", run.Target);
        Assert.Equal([Plain(First)], run.Commits);
        Assert.Null(update.Question);
    }

    [Fact]
    public void GivenADetachedHead_WhenPicking_ThenItIsAllowedAndNamesTheDetachedHead()
    {
        var read = new PickCommitsRead(Tab, PickKind.CherryPick, [Plain(First)]);

        var update = PickReducer.Reduce(read, Context(head: new HeadState(HeadKind.Detached, null, First)));

        Assert.Equal("the detached HEAD", Assert.IsType<RunPickEffect>(Assert.Single(update.Effects)).Target);
    }

    [Fact]
    public void GivenAMergeCommit_WhenTheCommitsAreRead_ThenTheMainlineQuestionOpensInInsertMode()
    {
        var read = new PickCommitsRead(Tab, PickKind.Revert, [Merge(First)]);

        var update = PickReducer.Reduce(read, Context(busy: true));

        var question = update.Question!;
        Assert.Equal(Tab, question.TabId);
        Assert.Equal(PickKind.Revert, question.Kind);
        Assert.Equal(2, question.MainlineCount);
        Assert.Equal(2, question.UsableParents);
        Assert.Equal(Merge(First).Hash, question.Merge.Hash);
        Assert.Equal(KeyMode.Insert, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenAPlainCommitAmongTheCommits_WhenTheQuestionOpens_ThenOnlyParentOneIsUsable()
    {
        var read = new PickCommitsRead(Tab, PickKind.Revert, [Plain(Second), Merge(First)]);

        var update = PickReducer.Reduce(read, Context(busy: true));

        Assert.Equal(1, update.Question!.UsableParents);
        Assert.Equal(2, update.Question.MainlineCount);
    }

    [Fact]
    public void GivenTheTabIsNotActive_WhenAMergeCommitIsRead_ThenNoQuestionOpensAndTheUserIsTold()
    {
        var read = new PickCommitsRead(Tab, PickKind.Revert, [Merge(First)]);

        var update = PickReducer.Reduce(read, Context(active: false));

        Assert.Equal("The tab changed: nothing was done", Warning(update.Effects));
        Assert.Null(update.Question);
    }

    [Fact]
    public void GivenAnOperationStartedWhileReading_WhenTheCommitsArrive_ThenNothingRuns()
    {
        var read = new PickCommitsRead(Tab, PickKind.CherryPick, [Plain(First)]);

        var update = PickReducer.Reduce(read, Context(operation: Stopped(RepositoryOperation.Rebase)));

        Assert.Contains("rebase is in progress", Warning(update.Effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheQuestion_WhenAParentIsChosen_ThenItClosesAndThePickRunsWithThatMainline()
    {
        var question = Question(Merge(First));

        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, 2), Context(question: question));

        Assert.Null(update.Question);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(update.Effects[0]).Mode);
        var run = Assert.IsType<RunPickEffect>(update.Effects[1]);
        Assert.Equal(2, run.Mainline);
        Assert.Equal(PickKind.Revert, run.PickKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void GivenTheQuestion_WhenAParentOutOfRangeIsChosen_ThenItStaysOpenAndNothingRuns(int parent)
    {
        var question = Question(Merge(First));

        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, parent), Context(question: question));

        Assert.Equal("Choose a parent from 1 to 2 (Esc cancels)", Warning(update.Effects));
        Assert.Same(question, update.Question);
    }

    [Fact]
    public void GivenAMergeAndAPlainCommit_WhenParentTwoIsChosen_ThenItIsRefusedAndTheQuestionStays()
    {
        var question = Question(Merge(First), Plain(Second));

        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, 2), Context(question: question));

        Assert.Contains("every commit is a merge", Warning(update.Effects), StringComparison.Ordinal);
        Assert.Same(question, update.Question);
    }

    [Fact]
    public void GivenAMergeAndAPlainCommit_WhenParentOneIsChosen_ThenThePickRuns()
    {
        var question = Question(Merge(First), Plain(Second));

        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, 1), Context(question: question));

        Assert.Equal(1, Assert.Single(update.Effects.OfType<RunPickEffect>()).Mainline);
    }

    [Fact]
    public void GivenNoQuestion_WhenAParentIsChosen_ThenNothingHappens()
    {
        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, 1), Context());

        Assert.Empty(update.Effects);
        Assert.Null(update.Question);
    }

    [Fact]
    public void GivenTheQuestion_WhenAnotherOperationStartedMeanwhile_ThenAChosenParentIsRefusedAndTheQuestionCloses()
    {
        var question = Question(Merge(First));
        var context = Context(operation: Stopped(RepositoryOperation.Merge), question: question);

        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, 1), context);

        Assert.Null(update.Question);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
        Assert.Contains("merge is in progress", Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenTheQuestion_WhenAPickRunsMeanwhile_ThenAChosenParentIsRefusedAsBusy()
    {
        var context = Context(busy: true, question: Question(Merge(First)));

        var update = PickReducer.Reduce(new PickMainlineChosen(Tab, 1), context);

        Assert.Null(update.Question);
        Assert.Equal(PickReducer.BusyNotice, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenTheQuestion_WhenItIsCancelled_ThenItClosesAndTheModeReturns()
    {
        var update = PickReducer.Reduce(new PickMainlineCancelled(Tab), Context(question: Question(Merge(First))));

        Assert.Null(update.Question);
        Assert.Equal(KeyMode.Normal, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenNoQuestion_WhenCancelled_ThenNothingHappens()
    {
        var update = PickReducer.Reduce(new PickMainlineCancelled(Tab), Context());

        Assert.Empty(update.Effects);
    }

    [Theory]
    [InlineData(PickOutcome.Completed, StatusSeverity.Info, "Cherry-picked ")]
    [InlineData(PickOutcome.NothingToApply, StatusSeverity.Info, "Nothing to cherry-pick")]
    [InlineData(PickOutcome.Conflicted, StatusSeverity.Warning, "The cherry-pick of ")]
    public void GivenGitRan_WhenItFinished_ThenTheStatusTellsTheOutcomeAndTheTabRefreshes(
        PickOutcome outcome,
        StatusSeverity severity,
        string start)
    {
        var result = new PickResult(outcome, Second, First);

        var update = PickReducer.Reduce(
            new PickFinished(Tab, PickKind.CherryPick, result, "main", "1 commit"),
            Context(busy: true));

        var post = Assert.IsType<PostStatusEffect>(update.Effects[0]);
        Assert.Equal(severity, post.Severity);
        Assert.StartsWith(start, post.Text, StringComparison.Ordinal);
        Assert.Equal(new RefreshTabEffect(Tab), update.Effects[1]);
    }

    [Fact]
    public void GivenACompletedPick_WhenItFinished_ThenTheStatusNamesTheOldTipForTheUndo()
    {
        var result = new PickResult(PickOutcome.Completed, Second, First);

        var update = PickReducer.Reduce(
            new PickFinished(Tab, PickKind.Revert, result, "main", "2 commits"),
            Context(busy: true));

        var text = Assert.IsType<PostStatusEffect>(update.Effects[0]).Text;
        Assert.Equal(
            "Reverted 2 commits onto main (2222222), was 1111111; undo: git reset --hard 1111111 (the reflog has it)",
            text);
    }

    [Fact]
    public void GivenAFailedPick_WhenItFailed_ThenTheErrorIsShownAndTheTabRefreshes()
    {
        var update = PickReducer.Reduce(new PickFailed(Tab, "Could not revert x"), Context(busy: true));

        Assert.Equal(new PostStatusEffect("Could not revert x", StatusSeverity.Error), update.Effects[0]);
        Assert.Equal(new RefreshTabEffect(Tab), update.Effects[1]);
    }

    [Fact]
    public void GivenCommitsThatCouldNotBeRead_WhenItFailed_ThenTheErrorIsShownWithoutARefresh()
    {
        var update = PickReducer.Reduce(new PickReadFailed(Tab, "no such commit"), Context(busy: true));

        Assert.Equal(new PostStatusEffect("no such commit", StatusSeverity.Error), Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenACancelledOperation_WhenItEnds_ThenNothingIsSaid()
    {
        var update = PickReducer.Reduce(new PickCancelled(Tab), Context(busy: true));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenEveryConcretePickCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<PickCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                PickReducer.Reduce(command, Context());
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in PickReducer");
            }
            catch (Exception)
            {
                // An uninitialised command may fail on its null fields; only a missing case matters here.
            }
        }
    }

    private static PickRequested Request(PickKind kind, SidebarRowKind? row, params string[] hashes) =>
        new(Tab, kind, new PickTarget(row, [.. hashes]));
}
