using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Rebase.RebaseTestData;

namespace Gitunia.Core.Tests.Rebase;

public class QuickRebaseReducerTests
{
    [Theory]
    [InlineData(QuickRebaseKind.Fixup)]
    [InlineData(QuickRebaseKind.Reword)]
    [InlineData(QuickRebaseKind.Drop)]
    public void GivenAGraphCommit_WhenAskingAQuickRebase_ThenItIsPlanned(QuickRebaseKind kind)
    {
        var effects = Reduce(new QuickRebaseRequested(Tab, kind, Target));

        var plan = Assert.IsType<PlanQuickRebaseEffect>(Assert.Single(effects));
        Assert.Equal(kind, plan.QuickKind);
        Assert.Equal(Target, plan.CommitHash);
    }

    [Fact]
    public void GivenNothingSelected_WhenDropping_ThenNothingIsPlanned()
    {
        var effects = Reduce(new QuickRebaseRequested(Tab, QuickRebaseKind.Drop, null));

        Assert.Equal("Select a commit in the graph first", Warning(effects));
    }

    [Fact]
    public void GivenARunningRebaseOperation_WhenAskingAQuickRebase_ThenItIsRefusedAsBusy()
    {
        var request = new QuickRebaseRequested(Tab, QuickRebaseKind.Drop, Target);

        var effects = RebaseReducer.Reduce(request, Context(busy: true));

        Assert.Equal(RebaseReducer.BusyNotice, Warning(effects));
    }

    [Fact]
    public void GivenTrackedChanges_WhenDropping_ThenItIsRefusedBeforeAnyQuestion()
    {
        var tab = Repository(entries: [Modified("a.txt")]);
        var request = new QuickRebaseRequested(Tab, QuickRebaseKind.Drop, Target);

        var effects = RebaseReducer.Reduce(request, Context(tab));

        Assert.Contains("1 file with uncommitted changes", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADetachedHead_WhenDropping_ThenItIsRefused()
    {
        var tab = Repository(head: new HeadState(HeadKind.Detached, null, Tip));
        var request = new QuickRebaseRequested(Tab, QuickRebaseKind.Drop, Target);

        var effects = RebaseReducer.Reduce(request, Context(tab));

        Assert.Contains("HEAD is detached", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFailedPlan_WhenItArrives_ThenTheReasonIsShownAsAnError()
    {
        var effects = Reduce(new QuickRebasePlanFailed(Tab, "Could not fix up 2222222: the root has no parent"));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
    }

    [Fact]
    public void GivenADropPlan_WhenItArrives_ThenTheConfirmNamesTheCommitTheReplayedCommitsAndTheUndo()
    {
        var effects = Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop, later: 2)));

        var show = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Drop commit", show.Prompt.Title);
        Assert.Equal(
            "Drop 2222222 \"Add feature\" from main? Its changes disappear from the branch.",
            show.Prompt.Lines[0]);
        Assert.Equal("2 later commits of main are replayed and get new hashes.", show.Prompt.Lines[1]);
        Assert.Contains(UndoNotes.ReflogKey, show.Prompt.Consequence, StringComparison.Ordinal);
        Assert.DoesNotContain("git reset", show.Prompt.Consequence, StringComparison.Ordinal);
        var target = Assert.IsType<QuickRebaseConfirmed>(show.OnAccepted).Target;
        Assert.Null(target.NewSubject);
        Assert.Equal("main", target.BranchName);
    }

    [Fact]
    public void GivenTheTipCommit_WhenDropping_ThenTheConfirmSaysItIsTheTip()
    {
        var effects = Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop, later: 0)));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Equal("It is the tip of main: the branch gets a new tip commit hash.", lines[1]);
    }

    [Fact]
    public void GivenOneLaterCommit_WhenDropping_ThenTheConfirmIsSingular()
    {
        var effects = Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop, later: 1)));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Equal("1 later commit of main is replayed and gets new hashes.", lines[1]);
    }

    [Fact]
    public void GivenAFixupPlan_WhenItArrives_ThenTheConfirmNamesTheCommitItMeldsInto()
    {
        var effects = Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Fixup)));

        var show = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Fix up commit", show.Prompt.Title);
        Assert.StartsWith(
            "Fix up 2222222 \"Add feature\" into 3333333 \"Older\"?",
            show.Prompt.Lines[0],
            StringComparison.Ordinal);
    }

    [Fact]
    public void GivenABranchWithAnUpstream_WhenTheConfirmOpens_ThenItWarnsAboutTheForcePush()
    {
        var tab = Repository(refs: [Branch("main", Tip, "refs/remotes/origin/main", 0)]);

        var effects = RebaseReducer.Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop)), Context(tab));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Contains(
            lines,
            line => line.Contains("All of its commits are already on origin/main", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenARewordPlan_WhenItArrives_ThenTheSubjectPromptOpensWithTheCurrentSubject()
    {
        var plan = Plan(QuickRebaseKind.Reword);

        var effects = Reduce(new QuickRebasePlanned(Tab, plan));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Reword commit", request.Title);
        Assert.Equal("New subject for 2222222; the rest of the message is kept", request.Context);
        Assert.Equal("next", request.AcceptLabel);
        Assert.Equal("Add feature", request.InitialText);
        Assert.Equal(Tab, request.Owner);
        var answered = request.OnAccepted.Answered(new PromptAnswer("typed", null));
        Assert.Equal(new QuickRebaseSubjectAccepted(Tab, plan, "typed"), answered);
    }

    [Fact]
    public void GivenARewordPlanOfATabNotShown_WhenItArrives_ThenNoPromptOpens()
    {
        var planned = new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Reword));

        var effects = RebaseReducer.Reduce(planned, Context(active: false));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenHeadMovedWhilePlanning_WhenThePlanArrives_ThenNothingIsAsked()
    {
        var tab = Repository(head: OnBranch(commit: Older));

        var effects = RebaseReducer.Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop)), Context(tab));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenAnOverlayOpenedWhilePlanning_WhenThePlanArrives_ThenTheConfirmIsDropped()
    {
        var effects = RebaseReducer.Reduce(
            new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop)),
            Context(overlay: true));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenAnOperationStartedWhilePlanning_WhenThePlanArrives_ThenNothingIsAsked()
    {
        var tab = Repository(operation: Stopped(RepositoryOperation.Rebase));

        var effects = RebaseReducer.Reduce(new QuickRebasePlanned(Tab, Plan(QuickRebaseKind.Drop)), Context(tab));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenTheSubjectPrompt_WhenANewSubjectIsAccepted_ThenTheConfirmNamesIt()
    {
        var plan = Plan(QuickRebaseKind.Reword);

        var effects = Reduce(new QuickRebaseSubjectAccepted(Tab, plan, "  Better subject "));

        var show = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Reword commit", show.Prompt.Title);
        Assert.Equal(
            "Reword 2222222 \"Add feature\" to \"Better subject\"? The rest of its message is kept.",
            show.Prompt.Lines[0]);
        Assert.Equal("Better subject", Assert.IsType<QuickRebaseConfirmed>(show.OnAccepted).Target.NewSubject);
    }

    [Theory]
    [InlineData("", "The subject is empty: type one or Esc to cancel")]
    [InlineData("   ", "The subject is empty: type one or Esc to cancel")]
    [InlineData("Add feature", "The subject is unchanged: edit it or Esc to cancel")]
    [InlineData(" Add feature ", "The subject is unchanged: edit it or Esc to cancel")]
    public void GivenTheSubjectPrompt_WhenTheSubjectIsEmptyOrUnchanged_ThenItOpensAgainWithAReason(
        string typed,
        string notice)
    {
        var plan = Plan(QuickRebaseKind.Reword);

        var effects = Reduce(new QuickRebaseSubjectAccepted(Tab, plan, typed));

        Assert.Equal(notice, Assert.IsType<PostStatusEffect>(effects[0]).Text);
        var request = Assert.IsType<ShowPromptEffect>(effects[1]).Request;
        Assert.Equal(typed, request.InitialText);
        Assert.Equal("Reword commit", request.Title);
        Assert.DoesNotContain(effects, effect => effect is OperationEffect or ShowConfirmEffect);
    }

    [Fact]
    public void GivenTheSubjectPrompt_WhenHeadMovedMeanwhile_ThenNoConfirmOpens()
    {
        var tab = Repository(head: OnBranch(commit: Older));

        var effects = RebaseReducer.Reduce(
            new QuickRebaseSubjectAccepted(Tab, Plan(QuickRebaseKind.Reword), "New"),
            Context(tab));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenTheConfirm_WhenAccepted_ThenTheQuickRebaseRunsWithThePlanTheUserSaw()
    {
        var target = new QuickRebaseTarget(Tab, Plan(QuickRebaseKind.Reword), "main", "New");

        var effects = Reduce(new QuickRebaseConfirmed(Tab, target));

        Assert.Equal(target, Assert.IsType<RunQuickRebaseEffect>(Assert.Single(effects)).Target);
    }

    [Fact]
    public void GivenTheTabBecameBusyWhileAsking_WhenConfirming_ThenNothingIsRebased()
    {
        var target = new QuickRebaseTarget(Tab, Plan(QuickRebaseKind.Drop), "main", null);

        var effects = RebaseReducer.Reduce(new QuickRebaseConfirmed(Tab, target), Context(busy: true));

        Assert.Equal($"{RebaseReducer.BusyNotice}; nothing was rebased", Warning(effects));
    }

    [Fact]
    public void GivenHeadMovedWhileAsking_WhenConfirming_ThenNothingIsRebased()
    {
        var target = new QuickRebaseTarget(Tab, Plan(QuickRebaseKind.Drop), "main", null);
        var tab = Repository(head: OnBranch(commit: Older));

        var effects = RebaseReducer.Reduce(new QuickRebaseConfirmed(Tab, target), Context(tab));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenAnOperationStartedWhileAsking_WhenConfirming_ThenItIsRefused()
    {
        var target = new QuickRebaseTarget(Tab, Plan(QuickRebaseKind.Drop), "main", null);
        var tab = Repository(operation: Stopped(RepositoryOperation.Merge));

        var effects = RebaseReducer.Reduce(new QuickRebaseConfirmed(Tab, target), Context(tab));

        Assert.Contains("merge is in progress", Warning(effects), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(QuickRebaseKind.Drop, "Dropped 2222222 \"Add feature\" on main (3333333), was 1111111")]
    [InlineData(QuickRebaseKind.Fixup, "Fixed up 2222222 \"Add feature\" on main (3333333), was 1111111")]
    [InlineData(QuickRebaseKind.Reword, "Reworded 2222222 \"Add feature\" on main (3333333), was 1111111")]
    public void GivenAQuickRebaseThatCompleted_WhenItFinished_ThenTheStatusNamesTheUndoAndTheTabRefreshes(
        QuickRebaseKind kind,
        string start)
    {
        var target = new QuickRebaseTarget(Tab, Plan(kind), "main", null);
        var result = new InteractiveRebaseResult(OperationOutcome.Completed, Older, Tip);

        var effects = Reduce(new QuickRebaseFinished(Tab, target, result));

        var post = Assert.IsType<PostStatusEffect>(effects[0]);
        Assert.Equal(StatusSeverity.Info, post.Severity);
        Assert.Equal(start, post.Text);
        Assert.Equal($"Undo: git reset --keep {Tip} (or ORIG_HEAD, the reflog)", post.Detail);
        Assert.Equal(new RefreshTabEffect(Tab), effects[1]);
    }

    [Fact]
    public void GivenADropThatConflicts_WhenItFinished_ThenTheStatusWarnsAndPointsToContinueAndAbort()
    {
        var target = new QuickRebaseTarget(Tab, Plan(QuickRebaseKind.Drop), "main", null);
        var result = new InteractiveRebaseResult(OperationOutcome.Conflicted, null, Tip);

        var effects = Reduce(new QuickRebaseFinished(Tab, target, result));

        var post = Assert.IsType<PostStatusEffect>(effects[0]);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.StartsWith("Drop of 2222222", post.Text, StringComparison.Ordinal);
        Assert.Contains("abort, back to 1111111", post.Text, StringComparison.Ordinal);
    }

    private static ImmutableArray<AppEffect> Reduce(RebaseCommand command) =>
        RebaseReducer.Reduce(command, Context());
}
