using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Rebase;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Rebase.RebaseTestData;

namespace Gitunia.Core.Tests.Rebase;

public class RebaseReducerTests
{
    [Fact]
    public void GivenABranchRow_WhenRebasing_ThenTheMergesAreCountedForThatBranch()
    {
        var effects = Reduce(Request(SidebarRowKind.LocalBranch, "topic", Target));

        var count = Assert.IsType<CountRebaseMergesEffect>(Assert.Single(effects));
        Assert.Equal(new RebaseTarget(Tab, Target, "topic", "main", Tip), count.Target);
    }

    [Fact]
    public void GivenAGraphCommit_WhenRebasing_ThenTheTargetIsNamedByItsShortHash()
    {
        var effects = Reduce(Request(null, null, Target));

        Assert.Equal("2222222", Assert.IsType<CountRebaseMergesEffect>(Assert.Single(effects)).Target.Label);
    }

    [Theory]
    [InlineData(SidebarRowKind.Stash)]
    [InlineData(SidebarRowKind.Worktree)]
    [InlineData(SidebarRowKind.Section)]
    public void GivenASidebarRowThatIsNotARef_WhenRebasing_ThenNothingIsAsked(SidebarRowKind kind)
    {
        var effects = Reduce(Request(kind, "row", Target));

        Assert.Contains("Select a commit", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNothingSelected_WhenRebasing_ThenNothingIsAsked()
    {
        var effects = Reduce(Request(null, null, null));

        Assert.Contains("Select a commit", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARunningRebaseOperation_WhenRebasingAgain_ThenItIsRefusedAsBusy()
    {
        var effects = RebaseReducer.Reduce(Request(null, null, Target), Context(busy: true));

        Assert.Equal(RebaseReducer.BusyNotice, Warning(effects));
    }

    [Theory]
    [InlineData(RepositoryOperation.Rebase, "rebase")]
    [InlineData(RepositoryOperation.Merge, "merge")]
    public void GivenAnOperationInProgress_WhenRebasing_ThenItIsRefusedNamingItAndNothingIsAsked(
        RepositoryOperation kind,
        string name)
    {
        var tab = Repository(operation: Stopped(kind));

        var effects = RebaseReducer.Reduce(Request(null, null, Target), Context(tab));

        Assert.Contains($"finish or abort the {name} first", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTrackedChanges_WhenRebasing_ThenItIsRefusedCountingTheFilesAndUntrackedFilesAreIgnored()
    {
        var tab = Repository(entries: [Modified("a.txt"), Modified("b.txt"), Untracked("c.txt")]);

        var effects = RebaseReducer.Reduce(Request(null, null, Target), Context(tab));

        Assert.Contains("2 files with uncommitted changes", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenOnlyUntrackedFiles_WhenRebasing_ThenTheMergesAreCounted()
    {
        var tab = Repository(entries: [Untracked("c.txt")]);

        var effects = RebaseReducer.Reduce(Request(null, null, Target), Context(tab));

        Assert.IsType<CountRebaseMergesEffect>(Assert.Single(effects));
    }

    [Fact]
    public void GivenRebaseAutoStash_WhenRebasingWithTrackedChanges_ThenTheConfirmOpensWithTheStashNote()
    {
        var tab = Repository(autoStash: true, entries: [Modified("a.txt")]);
        var counted = new RebaseMergesCounted(Tab, OntoTarget(), 0);

        var effects = RebaseReducer.Reduce(counted, Context(tab));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt;
        Assert.Contains(
            confirm.Lines,
            line => line.Contains("1 file with uncommitted changes", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenADetachedHead_WhenRebasing_ThenItIsRefused()
    {
        var tab = Repository(head: new HeadState(HeadKind.Detached, null, Tip));

        var effects = RebaseReducer.Reduce(Request(null, null, Target), Context(tab));

        Assert.Contains("HEAD is detached", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCurrentCommit_WhenRebasingOntoIt_ThenItIsRefused()
    {
        var effects = Reduce(Request(null, null, Tip));

        Assert.Contains("already at that commit", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCount_WhenItArrives_ThenTheConfirmNamesBothSidesAndTheUndoAndAsksToRebase()
    {
        var counted = new RebaseMergesCounted(Tab, OntoTarget(), 0);

        var effects = Reduce(counted);

        var show = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Rebase main onto topic?", show.Prompt.Lines[0]);
        Assert.Contains(UndoNotes.Reflog, show.Prompt.Consequence, StringComparison.Ordinal);
        Assert.DoesNotContain("git reset", show.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(new RebaseOntoConfirmed(Tab, OntoTarget()), show.OnAccepted);
        Assert.Equal(RebaseReducer.TooSmallNotice, show.UnavailableNotice);
        Assert.DoesNotContain(show.Prompt.Lines, line => line.Contains("force push", StringComparison.Ordinal));
        Assert.DoesNotContain(show.Prompt.Lines, line => line.Contains("flattened", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenABranchWithAnUpstream_WhenTheCountArrives_ThenTheConfirmWarnsAboutTheForcePush()
    {
        var tab = Repository(refs: [Branch("main", Tip, "refs/remotes/origin/main", 1)]);

        var effects = RebaseReducer.Reduce(new RebaseMergesCounted(Tab, OntoTarget(), 0), Context(tab));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Contains(lines, line => line.Contains("a force push is needed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, "1 merge commit in that range will be flattened")]
    [InlineData(3, "3 merge commits in that range will be flattened")]
    public void GivenMergesInTheRange_WhenTheCountArrives_ThenTheConfirmWarnsThatTheyAreFlattened(
        int merges,
        string text)
    {
        var effects = Reduce(new RebaseMergesCounted(Tab, OntoTarget(), merges));

        var lines = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects)).Prompt.Lines;
        Assert.Contains(lines, line => line.StartsWith(text, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenHeadMovedWhileCounting_WhenTheCountArrives_ThenNoConfirmOpens()
    {
        var tab = Repository(head: OnBranch(commit: Older));

        var effects = RebaseReducer.Reduce(new RebaseMergesCounted(Tab, OntoTarget(), 0), Context(tab));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenAnOperationStartedWhileCounting_WhenTheCountArrives_ThenNoConfirmOpens()
    {
        var tab = Repository(operation: Stopped(RepositoryOperation.Merge));

        var effects = RebaseReducer.Reduce(new RebaseMergesCounted(Tab, OntoTarget(), 0), Context(tab));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenAnOverlayOpenedWhileCounting_WhenTheCountArrives_ThenNoConfirmReplacesIt()
    {
        var effects = RebaseReducer.Reduce(new RebaseMergesCounted(Tab, OntoTarget(), 0), Context(overlay: true));

        Assert.Equal(RebaseReducer.ChangedNotice, Warning(effects));
    }

    [Fact]
    public void GivenTheConfirm_WhenAccepted_ThenTheRebaseRunsOntoTheTargetTheUserSaw()
    {
        var effects = Reduce(new RebaseOntoConfirmed(Tab, OntoTarget()));

        var run = Assert.IsType<RunRebaseEffect>(Assert.Single(effects));
        Assert.Equal(OntoTarget(), run.Target);
    }

    [Fact]
    public void GivenTheTabBecameBusyWhileAsking_WhenConfirming_ThenNothingIsRebased()
    {
        var effects = RebaseReducer.Reduce(new RebaseOntoConfirmed(Tab, OntoTarget()), Context(busy: true));

        Assert.Equal($"{RebaseReducer.BusyNotice}; nothing was rebased", Warning(effects));
    }

    [Fact]
    public void GivenAnOperationStartedWhileAsking_WhenConfirming_ThenItIsRefusedAndNothingRuns()
    {
        var tab = Repository(operation: Stopped(RepositoryOperation.Rebase));

        var effects = RebaseReducer.Reduce(new RebaseOntoConfirmed(Tab, OntoTarget()), Context(tab));

        Assert.Contains("rebase is in progress", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARebaseThatCompleted_WhenItFinished_ThenTheStatusNamesTheUndoAndTheTabRefreshes()
    {
        var result = new OperationResult(OperationOutcome.Completed, Older);

        var effects = Reduce(new RebaseFinished(Tab, OntoTarget(), result));

        var post = Assert.IsType<PostStatusEffect>(effects[0]);
        Assert.Equal(StatusSeverity.Info, post.Severity);
        Assert.Equal("Rebased main onto topic (3333333), was 1111111", post.Text);
        Assert.Equal($"Undo: git reset --keep {Tip} (or ORIG_HEAD, the reflog)", post.Detail);
        Assert.Equal(new RefreshTabEffect(Tab), effects[1]);
    }

    [Fact]
    public void GivenARebaseThatChangedNothing_WhenItFinished_ThenTheStatusSaysSo()
    {
        var result = new OperationResult(OperationOutcome.Completed, Tip);

        var effects = Reduce(new RebaseFinished(Tab, OntoTarget(), result));

        Assert.Equal(
            "main is already based on topic: nothing changed",
            Assert.IsType<PostStatusEffect>(effects[0]).Text);
    }

    [Fact]
    public void GivenARebaseThatStoppedOnConflicts_WhenItFinished_ThenAWarningPointsToContinueAndAbort()
    {
        var result = new OperationResult(OperationOutcome.Conflicted, null);

        var effects = Reduce(new RebaseFinished(Tab, OntoTarget(), result));

        var post = Assert.IsType<PostStatusEffect>(effects[0]);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.Contains("<space>gc", post.Text, StringComparison.Ordinal);
        Assert.Contains("abort, back to 1111111", post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFailedRebase_WhenItFailed_ThenTheReasonIsShownAsAnErrorAndTheTabRefreshes()
    {
        var effects = Reduce(new RebaseFailed(Tab, "Could not rebase main onto topic: boom"));

        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(effects[0]).Severity);
        Assert.Equal(new RefreshTabEffect(Tab), effects[1]);
    }

    [Fact]
    public void GivenACancelledRebase_WhenItEnds_ThenNothingIsSaid()
    {
        Assert.Empty(Reduce(new RebaseCancelled(Tab)));
    }

    [Fact]
    public void GivenEveryConcreteRebaseCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<RebaseCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                RebaseReducer.Reduce(command, Context());
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in RebaseReducer");
            }
            catch (Exception)
            {
                // An uninitialised command may fail on its null fields; only a missing case matters here.
            }
        }
    }

    private static RebaseOntoRequested Request(SidebarRowKind? kind, string? name, string? commit) =>
        new(Tab, new RebaseSelection(kind, name, commit));

    private static ImmutableArray<AppEffect> Reduce(RebaseCommand command) =>
        RebaseReducer.Reduce(command, Context());
}
