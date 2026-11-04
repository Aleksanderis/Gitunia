using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Tags;

public class TagReducerTests
{
    private const string Commit = "1111111111111111111111111111111111111111";
    private const string Annotated = "2222222222222222222222222222222222222222";

    private static readonly TabId Tab = new(1);

    [Fact]
    public void GivenAGraphCommit_WhenCreating_ThenTheNamePromptIsRequestedAtIt()
    {
        var effects = Reduce(new TagCreateRequested(Tab, Graph(Commit)));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("New tag", request.Title);
        Assert.Equal("At 1111111", request.Context);
        Assert.Null(request.Choices);
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(new TagNameAnswer(Tab, Commit, "1111111"), request.OnAccepted);
    }

    [Fact]
    public void GivenASidebarBranch_WhenCreating_ThenThePromptNamesTheBranch()
    {
        var target = new TagTarget(SidebarRowKind.LocalBranch, "main", Commit);

        var effects = Reduce(new TagCreateRequested(Tab, target));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("At main", request.Context);
        Assert.Equal(new TagNameAnswer(Tab, Commit, "main"), request.OnAccepted);
    }

    [Fact]
    public void GivenNothingSelected_WhenCreating_ThenNoPromptIsRequestedAndTheWarningSaysSo()
    {
        var effects = Reduce(new TagCreateRequested(Tab, new TagTarget(null, null, null)));

        Assert.DoesNotContain(effects, effect => effect is ShowPromptEffect);
        Assert.Equal("Select a commit or a ref first", Warning(effects));
    }

    [Fact]
    public void GivenARunningOperation_WhenCreating_ThenItIsRefusedAsBusy()
    {
        var effects = Reduce(new TagCreateRequested(Tab, Graph(Commit)), busy: true);

        Assert.DoesNotContain(effects, effect => effect is ShowPromptEffect);
        Assert.Equal(TagReducer.BusyNotice, Warning(effects));
    }

    [Fact]
    public void GivenATagNameInUse_WhenTheNameIsAccepted_ThenNothingIsAskedOrCreated()
    {
        var effects = Reduce(new TagNameAccepted(Tab, Commit, "1111111", " v1 "), Tags(("v1", Commit, Commit)));

        Assert.Equal("Tag v1 already exists", Warning(effects));
        Assert.DoesNotContain(effects, effect => effect is TagEffect or ShowPromptEffect);
    }

    [Fact]
    public void GivenAnEmptyName_WhenTheNameIsAccepted_ThenNothingIsCreated()
    {
        var effects = Reduce(new TagNameAccepted(Tab, Commit, "1111111", "  "));

        Assert.Equal("No name: nothing was created", Warning(effects));
        Assert.DoesNotContain(effects, effect => effect is TagEffect or ShowPromptEffect);
    }

    [Fact]
    public void GivenAFreeName_WhenTheNameIsAccepted_ThenTheMessagePromptIsRequestedAndKeepsTheStartPoint()
    {
        var effects = Reduce(new TagNameAccepted(Tab, Commit, "1111111", " v2 "));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Tag message", request.Title);
        Assert.Equal("v2: empty makes a lightweight tag", request.Context);
        Assert.Equal(string.Empty, request.InitialText);
        Assert.Equal(new TagMessageAnswer(Tab, Commit, "1111111", "v2"), request.OnAccepted);
    }

    [Fact]
    public void GivenAMessage_WhenTheMessageIsAccepted_ThenAnAnnotatedTagIsRequested()
    {
        var effects = Reduce(new TagMessageAccepted(Tab, Commit, "1111111", "v2", " release "));

        var create = Assert.IsType<CreateTagEffect>(Assert.Single(effects));
        Assert.Equal("v2", create.Name);
        Assert.Equal(Commit, create.StartPoint);
        Assert.Equal("release", create.Message);
    }

    [Fact]
    public void GivenAnEmptyMessage_WhenTheMessageIsAccepted_ThenTheEffectCarriesNoMessage()
    {
        var effects = Reduce(new TagMessageAccepted(Tab, Commit, "1111111", "v2", string.Empty));

        Assert.Equal(string.Empty, Assert.IsType<CreateTagEffect>(Assert.Single(effects)).Message);
    }

    [Fact]
    public void GivenATagRow_WhenDeleting_ThenTheConfirmNamesTheTagAndYDeletesItByTheSeenObject()
    {
        var refs = Tags(("v1", Annotated, Commit));
        var target = new TagTarget(SidebarRowKind.Tag, "v1", Commit);

        var effects = Reduce(new TagDeleteRequested(Tab, target), refs);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Contains("v1", confirm.Prompt.Lines[0]);
        Assert.Equal(UndoNotes.FromHistory, confirm.Prompt.Consequence);
        Assert.Equal(new TagDeleteConfirmed(Tab, "v1", Annotated), confirm.OnAccepted);
    }

    [Fact]
    public void GivenTwoTagsAtTheCommit_WhenDeletingFromTheGraph_ThenNothingIsAsked()
    {
        var refs = Tags(("v1", Commit, Commit), ("v1-rc", Commit, Commit));

        var effects = Reduce(new TagDeleteRequested(Tab, Graph(Commit)), refs);

        Assert.Equal("2 tags point here: pick one in the sidebar", Warning(effects));
    }

    [Fact]
    public void GivenACommitWithoutTag_WhenDeleting_ThenTheWarningSaysToSelectATag()
    {
        var effects = Reduce(new TagDeleteRequested(Tab, Graph(Commit)));

        Assert.Equal("Select a tag (sidebar) or a commit with one tag on it", Warning(effects));
    }

    [Fact]
    public void GivenABranchRow_WhenDeleting_ThenItIsRefusedAsNotATag()
    {
        var target = new TagTarget(SidebarRowKind.LocalBranch, "main", Commit);

        var effects = Reduce(new TagDeleteRequested(Tab, target), Tags(("main", Commit, Commit)));

        Assert.Equal("Select a tag first", Warning(effects));
    }

    [Fact]
    public void GivenTheConfirmAnswered_WhenNothingRuns_ThenTheDeleteIsRequested()
    {
        var effects = Reduce(new TagDeleteConfirmed(Tab, "v1", Annotated));

        var delete = Assert.IsType<DeleteTagEffect>(Assert.Single(effects));
        Assert.Equal("v1", delete.Name);
    }

    [Fact]
    public void GivenTheConfirmAnswered_WhenAnotherOperationStartedMeanwhile_ThenNothingIsDeleted()
    {
        var effects = Reduce(new TagDeleteConfirmed(Tab, "v1", Annotated), busy: true);

        Assert.DoesNotContain(effects, effect => effect is TagEffect);
        Assert.Equal($"{TagReducer.BusyNotice}; nothing was deleted", Warning(effects));
    }

    [Fact]
    public void GivenATag_WhenPushing_ThenTheRemotesAreRead()
    {
        var refs = Tags(("v1", Commit, Commit));

        var effects = Reduce(new TagPushRequested(Tab, Graph(Commit)), refs);

        var list = Assert.IsType<ListTagRemotesEffect>(Assert.Single(effects));
        Assert.Equal("v1", list.Name);
        Assert.True(list.Push);
    }

    [Fact]
    public void GivenNoRemote_WhenTheRemotesLoad_ThenNoPromptIsRequestedAndTheWarningSaysSo()
    {
        var effects = Reduce(new TagRemotesLoaded(Tab, "v1", true, []));

        Assert.DoesNotContain(effects, effect => effect is ShowPromptEffect);
        Assert.Equal("There is no remote: add one first", Warning(effects));
    }

    [Fact]
    public void GivenTheTabWasLeft_WhenTheRemotesLoad_ThenNoPromptIsRequested()
    {
        var effects = Reduce(new TagRemotesLoaded(Tab, "v1", true, ["origin"]), active: false);

        Assert.Empty(effects);
    }

    [Fact]
    public void GivenTwoRemotes_WhenTheRemotesLoadForAPush_ThenAPickerOverThemIsRequested()
    {
        var effects = Reduce(new TagRemotesLoaded(Tab, "v1", true, ["origin", "backup"]));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Push tag", request.Title);
        Assert.Equal("Remote to push v1 to", request.Context);
        Assert.Equal(["origin", "backup"], request.Choices!.Value.Select(choice => choice.Value));
        Assert.Equal(TagReducer.NoRemoteMatch, request.NoMatch);
        Assert.Equal(new TagRemoteAnswer(Tab, "v1", true), request.OnAccepted);
    }

    [Fact]
    public void GivenARemoteDelete_WhenTheRemotesLoad_ThenThePickerSaysItDeletes()
    {
        var effects = Reduce(new TagRemotesLoaded(Tab, "v1", false, ["origin"]));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Delete tag on remote", request.Title);
        Assert.Equal("Remote to delete v1 from", request.Context);
        Assert.Equal(new TagRemoteAnswer(Tab, "v1", false), request.OnAccepted);
    }

    [Fact]
    public void GivenAChosenRemote_WhenAPushIsChosen_ThenOnlyThatTagGoesToThatRemote()
    {
        var effects = Reduce(new TagRemoteChosen(Tab, "v1", true, "backup"));

        var push = Assert.IsType<PushTagEffect>(Assert.Single(effects));
        Assert.Equal(("backup", "v1"), (push.Remote, push.Name));
    }

    [Fact]
    public void GivenARemote_WhenARemoteDeleteIsChosen_ThenTheConfirmNamesRemoteAndTagAndTheLocalTagStays()
    {
        var effects = Reduce(new TagRemoteChosen(Tab, "v1", false, "origin"));

        var confirm = Assert.Single(effects.OfType<ShowConfirmEffect>());
        Assert.Contains("v1", confirm.Prompt.Lines[0]);
        Assert.Contains("origin", confirm.Prompt.Lines[0]);
        Assert.Equal("The local tag stays.", confirm.Prompt.Lines[1]);
        Assert.Equal(new TagDeleteOnRemoteConfirmed(Tab, "origin", "v1"), confirm.OnAccepted);
    }

    [Fact]
    public void GivenASuccess_WhenReducing_ThenTheTabIsRefreshedAndTheTextIsPosted()
    {
        var effects = Reduce(new TagOperationSucceeded(Tab, "Created tag v1"));

        Assert.Contains(
            effects,
            effect => effect is PostStatusEffect { Text: "Created tag v1", Severity: StatusSeverity.Info });
        Assert.Contains(effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenAFailureThatNeedsCredentials_WhenReducing_ThenTheTerminalRetryIsOffered()
    {
        var retry = new TagRetry("Tag push", ["push", "origin", "refs/tags/v1"]);

        var effects = Reduce(new TagOperationFailed(Tab, "denied", retry));

        Assert.Contains(effects, effect => effect is PostStatusEffect { Severity: StatusSeverity.Error });
        var offer = Assert.Single(effects.OfType<OfferTerminalRetryEffect>());
        Assert.Equal("Tag push", offer.Noun);
    }

    [Fact]
    public void GivenAFailureWithoutCredentials_WhenReducing_ThenNoRetryIsOfferedButTheTabIsRefreshed()
    {
        var effects = Reduce(new TagOperationFailed(Tab, "rejected", null));

        Assert.DoesNotContain(effects, effect => effect is OfferTerminalRetryEffect);
        Assert.Contains(effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenTheRemotesFailed_WhenReducing_ThenTheErrorIsPostedWithoutRefreshing()
    {
        var effects = Reduce(new TagRemotesFailed(Tab, "no remotes"));

        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(Assert.Single(effects)).Severity);
    }

    [Fact]
    public void GivenACancelledOperation_WhenReducing_ThenNothingIsPosted()
    {
        var effects = Reduce(new TagOperationCancelled(Tab));

        Assert.Empty(effects);
    }

    [Fact]
    public void GivenEveryConcreteTagCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<TagCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                TagReducer.Reduce(command, Context([], true));
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in TagReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static ImmutableArray<AppEffect> Reduce(
        TagCommand command,
        ImmutableArray<GitRef>? refs = null,
        bool active = true,
        bool busy = false) =>
        TagReducer.Reduce(command, Context(refs ?? [], active, busy));

    private static TagContext Context(ImmutableArray<GitRef> refs, bool active, bool busy = false) =>
        new(Tab, Location(), refs, active, busy);

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-tag-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static TagTarget Graph(string commit) => new(null, null, commit);

    private static ImmutableArray<GitRef> Tags(params (string Name, string ObjectHash, string CommitHash)[] tags) =>
    [
        .. tags.Select(tag => new GitRef(
            $"refs/tags/{tag.Name}",
            tag.Name,
            RefKind.Tag,
            tag.ObjectHash,
            tag.CommitHash,
            null,
            null,
            null,
            false,
            false,
            null,
            null)),
    ];

    private static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects.OfType<PostStatusEffect>()));
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        return post.Text;
    }
}
