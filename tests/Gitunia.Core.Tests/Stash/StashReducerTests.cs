using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Xunit;

namespace Gitunia.Core.Tests.Stash;

public class StashReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly StashEntry Entry = new(0, "stash@{0}", "abc123", "On main: \"quoted\" work");
    private static readonly StashFileTarget Target = new(
        new StashEntry(0, "stash@{0}", "abc123", "wip"),
        new StashFileChange(
            new CommitFileChange("src/a.txt", null, FileChangeKind.Modified, null),
            StashFileOrigin.Tracked));

    [Theory]
    [InlineData(false, "Tracked changes, message optional")]
    [InlineData(true, "Tracked and untracked files, message optional")]
    public void GivenThePushKey_WhenRequested_ThenTheMessagePromptOpensForTheTab(bool untracked, string context)
    {
        var effects = Reduce(new StashPushRequested(Tab, untracked));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Stash changes", request.Title);
        Assert.Equal(context, request.Context);
        Assert.Equal("stash", request.AcceptLabel);
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(new StashMessageAnswer(Tab, untracked), request.OnAccepted);
    }

    [Fact]
    public void GivenAMessage_WhenTheAnswerIsReduced_ThenPushStartsWithTheTrimmedMessageAndTheUntrackedChoice()
    {
        var answer = new StashMessageAnswer(Tab, true).Answered(new PromptAnswer("  wip  ", null));

        var effects = Reduce(Assert.IsAssignableFrom<StashCommand>(answer));

        var push = Assert.IsType<PushStashEffect>(Assert.Single(effects));
        Assert.Equal("wip", push.Message);
        Assert.True(push.IncludeUntracked);
    }

    [Fact]
    public void GivenAnotherOperationRuns_WhenPushingOrAccepting_ThenItIsRefusedBusy()
    {
        Assert.Equal(StashReducer.BusyNotice, Warning(Reduce(new StashPushRequested(Tab, false), busy: true)));
        Assert.Equal(
            StashReducer.BusyNotice,
            Warning(Reduce(new StashMessageAccepted(Tab, false, "x"), busy: true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenAStashRowInTheSidebar_WhenApplyingOrPopping_ThenTheEffectCarriesTheEntryOfThatCommit(bool pop)
    {
        var effects = Reduce(new StashApplyRequested(Tab, "abc123", pop));

        var apply = Assert.IsType<ApplyStashEffect>(Assert.Single(effects));
        Assert.Equal(Entry, apply.Entry);
        Assert.Equal(pop, apply.Pop);
    }

    [Theory]
    [InlineData(PaneKind.Graph)]
    [InlineData(PaneKind.Detail)]
    public void GivenFocusOutsideTheSidebar_WhenApplyingPoppingOrDropping_ThenItIsRefusedWithoutAnEffect(PaneKind pane)
    {
        StashCommand[] commands =
        [
            new StashApplyRequested(Tab, "abc123", false),
            new StashApplyRequested(Tab, "abc123", true),
            new StashDropRequested(Tab, "abc123"),
        ];

        foreach (var command in commands)
        {
            Assert.Equal(StashReducer.NoStashNotice, Warning(Reduce(command, pane: pane)));
        }
    }

    [Fact]
    public void GivenNoStashRowOrAnUnknownCommit_WhenApplyingOrDropping_ThenItIsRefused()
    {
        Assert.Equal(StashReducer.NoStashNotice, Warning(Reduce(new StashApplyRequested(Tab, null, true))));
        Assert.Equal(StashReducer.NoStashNotice, Warning(Reduce(new StashApplyRequested(Tab, "gone", false))));
        Assert.Equal(StashReducer.NoStashNotice, Warning(Reduce(new StashDropRequested(Tab, "gone"))));
    }

    [Fact]
    public void GivenAnotherOperationRuns_WhenApplyingOrDropping_ThenItIsRefusedBusy()
    {
        var apply = Reduce(new StashApplyRequested(Tab, "abc123", false), busy: true);
        var drop = Reduce(new StashDropRequested(Tab, "abc123"), busy: true);

        Assert.Equal(StashReducer.BusyNotice, Warning(apply));
        Assert.Equal(StashReducer.BusyNotice, Warning(drop));
    }

    [Fact]
    public void GivenAStashRow_WhenDropping_ThenTheDialogNamesItAndTheUndoCommandAndNothingRunsYet()
    {
        var effects = Reduce(new StashDropRequested(Tab, "abc123"));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Drop stash", confirm.Prompt.Title);
        Assert.Equal(["Drop stash@{0}: On main: \"quoted\" work?"], confirm.Prompt.Lines);
        Assert.Equal("Undo: git stash store -m \"On main: \\\"quoted\\\" work\" abc123", confirm.Prompt.Consequence);
        Assert.Equal(new StashDropConfirmed(Tab, Entry), confirm.OnAccepted);
        Assert.Equal("The screen is too small to ask: nothing was dropped", confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenTheDropIsConfirmed_WhenReducing_ThenTheEntryTheUserSawIsDroppedEvenIfTheStackChanged()
    {
        var shifted = new StashEntry(1, "stash@{1}", "abc123", "moved");
        var context = Context(stashes: [new StashEntry(0, "stash@{0}", "other", "newer"), shifted]);

        var effects = StashReducer.Reduce(new StashDropConfirmed(Tab, shifted), context);

        Assert.Equal(shifted, Assert.IsType<DropStashEffect>(Assert.Single(effects)).Entry);
    }

    [Fact]
    public void GivenAnotherOperationStartedBeforeTheAnswer_WhenTheDropIsConfirmed_ThenNothingIsDropped()
    {
        var effects = Reduce(new StashDropConfirmed(Tab, Entry), busy: true);

        Assert.Equal($"{StashReducer.BusyNotice}; nothing was dropped", Warning(effects));
        Assert.DoesNotContain(effects, effect => effect is StashEffect);
    }

    [Fact]
    public void GivenAFileTarget_WhenRestoring_ThenTheStateIsCheckedFirst()
    {
        var effects = Reduce(new StashRestoreFileRequested(Tab, Target));

        Assert.Equal(Target, Assert.IsType<CheckStashRestoreEffect>(Assert.Single(effects)).Target);
    }

    [Fact]
    public void GivenNoFileUnderTheCursorOrABusyTab_WhenRestoring_ThenItIsRefusedWithoutGit()
    {
        Assert.Equal(StashReducer.NoFileNotice, Warning(Reduce(new StashRestoreFileRequested(Tab, null))));
        Assert.Equal(StashReducer.BusyNotice, Warning(Reduce(new StashRestoreFileRequested(Tab, Target), busy: true)));
    }

    [Fact]
    public void GivenLocalChanges_WhenTheCheckSaysOverwrites_ThenTheDialogNamesFileAndStashAndNothingRunsUntilYes()
    {
        var effects = Reduce(new StashRestoreChecked(Tab, Target, StashRestoreState.Overwrites));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Restore file from stash", confirm.Prompt.Title);
        Assert.Contains("src/a.txt", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Contains("stash@{0}", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Equal(new StashRestoreConfirmed(Tab, Target), confirm.OnAccepted);
        Assert.Equal("The screen is too small to ask: nothing was restored", confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenTheFileIsNotInTheWorkingTree_WhenTheCheckSaysCreates_ThenItRestoresWithoutAsking()
    {
        var effects = Reduce(new StashRestoreChecked(Tab, Target, StashRestoreState.Creates));

        Assert.Equal(Target, Assert.IsType<RestoreStashFileEffect>(Assert.Single(effects)).Target);
    }

    [Theory]
    [InlineData(StashRestoreState.Missing, StatusSeverity.Warning, "no longer in the stash list")]
    [InlineData(StashRestoreState.NotInStash, StatusSeverity.Warning, "holds no copy of src/a.txt")]
    [InlineData(StashRestoreState.Identical, StatusSeverity.Info, "already matches stash@{0}")]
    public void GivenNothingToRestore_WhenTheCheckEnds_ThenItSaysWhyAndWritesNothing(
        StashRestoreState state,
        StatusSeverity severity,
        string text)
    {
        var effects = Reduce(new StashRestoreChecked(Tab, Target, state));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal(severity, post.Severity);
        Assert.Contains(text, post.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnotherOperationStartedBeforeTheAnswer_WhenTheRestoreIsConfirmed_ThenNothingIsRestored()
    {
        var effects = Reduce(new StashRestoreConfirmed(Tab, Target), busy: true);

        Assert.Equal($"{StashReducer.BusyNotice}; nothing was restored", Warning(effects));
    }

    [Fact]
    public void GivenTheRestoreIsConfirmed_WhenReducing_ThenTheFileIsRestored()
    {
        var effects = Reduce(new StashRestoreConfirmed(Tab, Target));

        Assert.Equal(Target, Assert.IsType<RestoreStashFileEffect>(Assert.Single(effects)).Target);
    }

    [Fact]
    public void GivenASuccess_WhenReducing_ThenTheMessageIsPostedAndTheTabRefreshes()
    {
        var effects = Reduce(new StashOperationSucceeded(Tab, "Dropped", StatusSeverity.Warning));

        Assert.Equal(new PostStatusEffect("Dropped", StatusSeverity.Warning), effects[0]);
        Assert.Equal(new RefreshTabEffect(Tab), effects[1]);
    }

    [Fact]
    public void GivenAFailure_WhenReducing_ThenTheErrorIsPostedAndTheTabRefreshes()
    {
        var effects = Reduce(new StashOperationFailed(Tab, "Could not apply the stash: x"));

        Assert.Equal(new PostStatusEffect("Could not apply the stash: x", StatusSeverity.Error), effects[0]);
        Assert.Equal(new RefreshTabEffect(Tab), effects[1]);
    }

    [Fact]
    public void GivenACancel_WhenReducing_ThenNothingHappens()
    {
        Assert.Empty(Reduce(new StashOperationCancelled(Tab)));
    }

    [Fact]
    public void GivenEveryConcreteStashCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<StashCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                StashReducer.Reduce(command, Context());
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in StashReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static ImmutableArray<AppEffect> Reduce(
        StashCommand command,
        bool busy = false,
        PaneKind pane = PaneKind.Sidebar) =>
        StashReducer.Reduce(command, Context(busy: busy, pane: pane));

    private static StashContext Context(
        ImmutableArray<StashEntry>? stashes = null,
        bool busy = false,
        PaneKind pane = PaneKind.Sidebar) =>
        new(Tab, Location(), stashes ?? [Entry], pane, busy);

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-stash-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        return post.Text;
    }
}
