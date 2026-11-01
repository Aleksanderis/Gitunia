using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Branches.BranchTestData;

namespace Gitunia.Core.Tests.Branches;

// Create, detach, rename and the results; switch, delete and upstream have their own files.
public class BranchReducerTests
{
    public static TheoryData<RepositoryOperation, string> Operations =>
        new()
        {
            { RepositoryOperation.Merge, "merge" },
            { RepositoryOperation.Rebase, "rebase" },
            { RepositoryOperation.CherryPick, "cherry-pick" },
            { RepositoryOperation.Revert, "revert" },
        };

    [Fact]
    public void GivenAGraphCommit_WhenCreatingWithCheckout_ThenTheNamePromptOpensAtItAndKeepsTheStartPoint()
    {
        var effects = Reduce(new BranchCreateRequested(Tab, Graph(Topic), true), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("New branch", request.Title);
        Assert.Equal("At 2222222", request.Context);
        Assert.Equal("create", request.AcceptLabel);
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(new BranchNameAnswer(Tab, Topic, "2222222", true), request.OnAccepted);
    }

    [Fact]
    public void GivenAGraphCommit_WhenCreatingWithoutCheckout_ThenThePromptSaysNotCheckedOut()
    {
        var effects = Reduce(new BranchCreateRequested(Tab, Graph(Topic), false), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("At 2222222 (not checked out)", request.Context);
        Assert.Equal(new BranchNameAnswer(Tab, Topic, "2222222", false), request.OnAccepted);
    }

    [Fact]
    public void GivenASidebarBranchRow_WhenCreating_ThenTheStartPointIsTheFullRefName()
    {
        var local = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);
        var remote = new BranchTarget(SidebarRowKind.RemoteBranch, "origin/topic", Topic);

        var fromLocal = Assert.IsType<ShowPromptEffect>(
            Assert.Single(Reduce(new BranchCreateRequested(Tab, local, true), MainAndTopic()))).Request;
        var fromRemote = Assert.IsType<ShowPromptEffect>(
            Assert.Single(Reduce(new BranchCreateRequested(Tab, remote, true), MainAndTopic()))).Request;

        Assert.Equal(new BranchNameAnswer(Tab, "refs/heads/topic", "topic", true), fromLocal.OnAccepted);
        var expected = new BranchNameAnswer(Tab, "refs/remotes/origin/topic", "origin/topic", true);
        Assert.Equal(expected, fromRemote.OnAccepted);
    }

    [Fact]
    public void GivenASectionHeaderSelection_WhenCreating_ThenTheBranchStartsAtTheGraphCommit()
    {
        var section = new BranchTarget(SidebarRowKind.Section, null, Topic);

        var effects = Reduce(new BranchCreateRequested(Tab, section, true), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(new BranchNameAnswer(Tab, Topic, "2222222", true), request.OnAccepted);
    }

    [Fact]
    public void GivenNothingSelected_WhenCreating_ThenNoPromptOpensAndTheWarningSaysSo()
    {
        var effects = Reduce(new BranchCreateRequested(Tab, new BranchTarget(null, null, null), true), MainAndTopic());

        Assert.Equal("Select a commit or a branch first", Warning(effects));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void GivenAnOperationInProgress_WhenCreatingWithCheckout_ThenItIsRefusedAndNoPromptOpens(
        RepositoryOperation kind,
        string name)
    {
        var request = new BranchCreateRequested(Tab, Graph(Main), true);

        var effects = Reduce(request, MainAndTopic(), operation: Stopped(kind));

        var warning = Warning(effects);
        Assert.Contains($"finish or abort the {name} first", warning, StringComparison.Ordinal);
        Assert.Contains("<space>gc", warning, StringComparison.Ordinal);
        Assert.Contains("<space>ga", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMergeInProgress_WhenCreatingWithoutCheckout_ThenThePromptStillOpens()
    {
        var effects = Reduce(
            new BranchCreateRequested(Tab, Graph(Main), false),
            MainAndTopic(),
            operation: Stopped(RepositoryOperation.Merge));

        Assert.IsType<ShowPromptEffect>(Assert.Single(effects));
    }

    [Fact]
    public void GivenARunningBranchOperation_WhenCreating_ThenItIsRefusedAsBusy()
    {
        var effects = Reduce(new BranchCreateRequested(Tab, Graph(Main), false), MainAndTopic(), busy: true);

        Assert.Equal(BranchRefusals.BusyNotice, Warning(effects));
    }

    [Fact]
    public void GivenAReflogEntry_WhenCreatingThere_ThenThePromptNamesTheShortHash()
    {
        var effects = Reduce(new BranchCreateAtRequested(Tab, Topic, true), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("At 2222222", request.Context);
        Assert.Equal(new BranchNameAnswer(Tab, Topic, "2222222", true), request.OnAccepted);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void GivenAnOperationInProgress_WhenCreatingOrDetachingAtAReflogEntry_ThenBothAreRefused(
        RepositoryOperation kind,
        string name)
    {
        var operation = Stopped(kind);

        var creation = Reduce(new BranchCreateAtRequested(Tab, Main, true), MainAndTopic(), operation: operation);
        var detach = Reduce(new BranchDetachRequested(Tab, Main), MainAndTopic(), operation: operation);

        Assert.Contains($"finish or abort the {name} first", Warning(creation), StringComparison.Ordinal);
        Assert.Equal(Warning(creation), Warning(detach));
    }

    [Fact]
    public void GivenAReflogEntry_WhenDetaching_ThenTheCheckoutIsDetachedThereAndTheCursorFollows()
    {
        var effects = Reduce(new BranchDetachRequested(Tab, Topic), MainAndTopic());

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.DetachedCommit, effect.TargetKind);
        Assert.Equal(Topic, effect.Target);
        Assert.Equal(Topic, effect.Follow);
    }

    [Fact]
    public void GivenAFreeName_WhenTheNameIsAccepted_ThenTheCreateEffectCarriesWhatTheUserSaw()
    {
        var accepted = new BranchNameAccepted(Tab, "refs/heads/topic", "topic", true, " feature/new ");

        var effects = Reduce(accepted, MainAndTopic());

        var create = Assert.IsType<CreateBranchEffect>(Assert.Single(effects));
        Assert.Equal("feature/new", create.Name);
        Assert.Equal("refs/heads/topic", create.StartPoint);
        Assert.Equal("topic", create.Label);
        Assert.True(create.Checkout);
        Assert.Equal(Topic, create.Follow);
        Assert.Equal(Tab, create.TabId);
    }

    [Fact]
    public void GivenACommitStartPoint_WhenTheNameIsAccepted_ThenTheCursorFollowsToThatCommit()
    {
        var effects = Reduce(new BranchNameAccepted(Tab, Topic, "2222222", false, "old"), MainAndTopic());

        var create = Assert.IsType<CreateBranchEffect>(Assert.Single(effects));
        Assert.False(create.Checkout);
        Assert.Equal(Topic, create.Follow);
    }

    [Fact]
    public void GivenANameInUse_WhenTheNameIsAccepted_ThenNothingRunsAndTheWarningSaysSo()
    {
        var effects = Reduce(new BranchNameAccepted(Tab, Main, "1111111", true, "topic"), MainAndTopic());

        Assert.Equal("Branch topic already exists", Warning(effects));
    }

    [Fact]
    public void GivenAnEmptyName_WhenTheNameIsAccepted_ThenNothingIsCreated()
    {
        var effects = Reduce(new BranchNameAccepted(Tab, Main, "1111111", true, "  "), MainAndTopic());

        Assert.Equal("No name: nothing was created", Warning(effects));
    }

    [Fact]
    public void GivenAnOperationStartingWhileThePromptIsOpen_WhenAcceptingACheckoutName_ThenNothingIsCreated()
    {
        var accepted = new BranchNameAccepted(Tab, Main, "1111111", true, "new");

        var effects = Reduce(accepted, MainAndTopic(), operation: Stopped(RepositoryOperation.Merge));

        Assert.Contains("finish or abort the merge first", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOperationStartingWhileThePromptIsOpen_WhenAcceptingANameWithoutCheckout_ThenItIsCreated()
    {
        var accepted = new BranchNameAccepted(Tab, Main, "1111111", false, "new");

        var effects = Reduce(accepted, MainAndTopic(), operation: Stopped(RepositoryOperation.Merge));

        Assert.IsType<CreateBranchEffect>(Assert.Single(effects));
    }

    [Fact]
    public void GivenARunningOperation_WhenAcceptingAName_ThenItIsRefusedAsBusy()
    {
        var effects = Reduce(new BranchNameAccepted(Tab, Main, "1111111", false, "new"), MainAndTopic(), busy: true);

        Assert.Equal(BranchRefusals.BusyNotice, Warning(effects));
    }

    [Fact]
    public void GivenABranchRow_WhenRenaming_ThenThePromptIsPrefilledWithTheName()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchRenameRequested(Tab, target), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Rename branch", request.Title);
        Assert.Equal("topic", request.Context);
        Assert.Equal("topic", request.InitialText);
        Assert.Equal("rename", request.AcceptLabel);
        Assert.Equal(new BranchRenameAnswer(Tab, "topic"), request.OnAccepted);
    }

    [Fact]
    public void GivenATagRow_WhenRenaming_ThenNoPromptOpens()
    {
        var refs = Refs(Local("main", Main, current: true), Tag("v1", Main));

        var effects = Reduce(new BranchRenameRequested(Tab, new BranchTarget(SidebarRowKind.Tag, "v1", Main)), refs);

        Assert.Equal("Select a local branch first", Warning(effects));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenASectionSelection_WhenRenamingOrDeleting_ThenNothingActsOnTheGraphBranch(bool rename)
    {
        var section = new BranchTarget(SidebarRowKind.Section, null, null);
        BranchCommand command = rename
            ? new BranchRenameRequested(Tab, section)
            : new BranchDeleteRequested(Tab, section);

        var effects = Reduce(command, MainAndTopic());

        Assert.Equal("Select a local branch first", Warning(effects));
    }

    [Fact]
    public void GivenAGraphCommitWithTwoBranches_WhenRenaming_ThenTheWarningAsksForTheSidebar()
    {
        var refs = Refs(Local("main", Main, current: true), Local("second", Main));

        var effects = Reduce(new BranchRenameRequested(Tab, Graph(Main)), refs);

        Assert.Equal("2 branches point here: pick one in the sidebar", Warning(effects));
    }

    [Fact]
    public void GivenAGraphCommitWithOneBranch_WhenRenaming_ThenThatBranchIsRenamed()
    {
        var effects = Reduce(new BranchRenameRequested(Tab, Graph(Topic)), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(new BranchRenameAnswer(Tab, "topic"), request.OnAccepted);
    }

    [Fact]
    public void GivenANewName_WhenTheRenameIsAccepted_ThenTheRenameEffectFollowsTheBranchCommit()
    {
        var effects = Reduce(new BranchRenameAccepted(Tab, "topic", " topic-2 "), MainAndTopic());

        var rename = Assert.IsType<RenameBranchEffect>(Assert.Single(effects));
        Assert.Equal("topic", rename.From);
        Assert.Equal("topic-2", rename.Name);
        Assert.Equal(Topic, rename.Follow);
    }

    [Theory]
    [InlineData("topic")]
    [InlineData("  ")]
    public void GivenTheSameOrAnEmptyName_WhenTheRenameIsAccepted_ThenNothingIsRenamed(string typed)
    {
        var effects = Reduce(new BranchRenameAccepted(Tab, "topic", typed), MainAndTopic());

        Assert.Equal("The name is unchanged: nothing was renamed", Warning(effects));
    }

    [Fact]
    public void GivenANameInUse_WhenTheRenameIsAccepted_ThenNothingIsRenamed()
    {
        var effects = Reduce(new BranchRenameAccepted(Tab, "topic", "main"), MainAndTopic());

        Assert.Equal("Branch main already exists", Warning(effects));
    }

    [Fact]
    public void GivenASuccess_WhenReduced_ThenTheTextIsShownAndTheTabIsRefreshed()
    {
        var effects = Reduce(new BranchOperationSucceeded(Tab, "Created branch x", Topic), MainAndTopic(), busy: true);

        var post = Assert.Single(effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Info, post.Severity);
        Assert.Equal("Created branch x", post.Text);
        Assert.Equal(Tab, Assert.Single(effects.OfType<RefreshTabEffect>()).TabId);
    }

    [Fact]
    public void GivenAFailure_WhenReduced_ThenTheErrorIsShownAndTheTabIsStillRefreshed()
    {
        var effects = Reduce(new BranchOperationFailed(Tab, "Could not switch: boom"), MainAndTopic(), busy: true);

        var post = Assert.Single(effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.Equal("Could not switch: boom", post.Text);
        Assert.Single(effects.OfType<RefreshTabEffect>());
    }

    [Fact]
    public void GivenACancelledOperation_WhenReduced_ThenNothingIsPostedOrRefreshed()
    {
        Assert.Empty(Reduce(new BranchOperationCancelled(Tab), MainAndTopic(), busy: true));
    }

    [Fact]
    public void GivenEveryCommand_WhenReduced_ThenNoneFallsThroughTheSwitch()
    {
        foreach (var command in ConcreteSubtypes.InstancesOf<BranchCommand>())
        {
            try
            {
                BranchReducer.Reduce(command, Context([]));
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in BranchReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static BranchTarget Graph(string commit) => new(null, null, commit);

    private static OperationInfo Stopped(RepositoryOperation kind) =>
        new(kind, null, null, null, null, null, null, null);
}
