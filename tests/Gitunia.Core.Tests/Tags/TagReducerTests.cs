using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
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
    public void GivenAGraphCommit_WhenCreating_ThenTheNamePromptOpensAtItInInsertMode()
    {
        var update = Reduce(TagState.Empty, new TagCreateRequested(Tab, Graph(Commit)));

        var prompt = update.State.Prompt!;
        Assert.Equal(TagPromptKind.CreateName, prompt.Kind);
        Assert.Equal(Commit, prompt.StartPoint);
        Assert.Equal("1111111", prompt.Label);
        Assert.Equal(KeyMode.Insert, Assert.IsType<SetKeyModeEffect>(Assert.Single(update.Effects)).Mode);
    }

    [Fact]
    public void GivenASidebarBranch_WhenCreating_ThenThePromptNamesTheBranch()
    {
        var target = new TagTarget(SidebarRowKind.LocalBranch, "main", Commit);

        var update = Reduce(TagState.Empty, new TagCreateRequested(Tab, target));

        Assert.Equal("main", update.State.Prompt!.Label);
    }

    [Fact]
    public void GivenNothingSelected_WhenCreating_ThenNoPromptOpensAndTheWarningSaysSo()
    {
        var update = Reduce(TagState.Empty, new TagCreateRequested(Tab, new TagTarget(null, null, null)));

        Assert.Null(update.State.Prompt);
        Assert.Equal("Select a commit or a ref first", Warning(update));
    }

    [Fact]
    public void GivenARunningOperation_WhenCreating_ThenItIsRefusedAsBusy()
    {
        var update = Reduce(TagState.Empty, new TagCreateRequested(Tab, Graph(Commit)), busy: true);

        Assert.Null(update.State.Prompt);
        Assert.Equal(TagReducer.BusyNotice, Warning(update));
    }

    [Fact]
    public void GivenATagNameInUse_WhenAcceptingTheName_ThenThePromptClosesWithoutAnEffectOnGit()
    {
        var state = Typed(TagPromptKind.CreateName, " v1 ");

        var update = Reduce(state, new TagPromptAccepted(Tab), Tags(("v1", Commit, Commit)));

        Assert.Null(update.State.Prompt);
        Assert.Equal("Tag v1 already exists", Warning(update));
        Assert.DoesNotContain(update.Effects, effect => effect is TagEffect);
    }

    [Fact]
    public void GivenAnEmptyName_WhenAccepting_ThenNothingIsCreated()
    {
        var update = Reduce(Typed(TagPromptKind.CreateName, "  "), new TagPromptAccepted(Tab));

        Assert.Null(update.State.Prompt);
        Assert.Equal("No name: nothing was created", Warning(update));
    }

    [Fact]
    public void GivenAFreeName_WhenAccepting_ThenTheMessagePromptOpensAndKeepsTheStartPoint()
    {
        var update = Reduce(Typed(TagPromptKind.CreateName, "v2"), new TagPromptAccepted(Tab));

        var prompt = update.State.Prompt!;
        Assert.Equal(TagPromptKind.CreateMessage, prompt.Kind);
        Assert.Equal("v2", prompt.NewName);
        Assert.Equal(string.Empty, prompt.Text);
        Assert.Equal(Commit, prompt.StartPoint);
    }

    [Fact]
    public void GivenAMessage_WhenAccepting_ThenAnAnnotatedTagIsRequested()
    {
        var state = Typed(TagPromptKind.CreateMessage, " release ", newName: "v2");

        var update = Reduce(state, new TagPromptAccepted(Tab));

        var create = Assert.Single(update.Effects.OfType<CreateTagEffect>());
        Assert.Equal("v2", create.Name);
        Assert.Equal(Commit, create.StartPoint);
        Assert.Equal("release", create.Message);
        Assert.Null(update.State.Prompt);
        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenAnEmptyMessage_WhenAccepting_ThenTheEffectCarriesNoMessage()
    {
        var state = Typed(TagPromptKind.CreateMessage, string.Empty, newName: "v2");

        var update = Reduce(state, new TagPromptAccepted(Tab));

        Assert.Equal(string.Empty, Assert.Single(update.Effects.OfType<CreateTagEffect>()).Message);
    }

    [Fact]
    public void GivenTheMessagePrompt_WhenCancelling_ThenNoTagIsRequested()
    {
        var update = Reduce(Typed(TagPromptKind.CreateMessage, "x", newName: "v2"), new TagPromptCancelled(Tab));

        Assert.Null(update.State.Prompt);
        Assert.DoesNotContain(update.Effects, effect => effect is TagEffect);
    }

    [Fact]
    public void GivenATagRow_WhenDeleting_ThenTheConfirmNamesTheTagAndYDeletesItByTheSeenObject()
    {
        var refs = Tags(("v1", Annotated, Commit));
        var target = new TagTarget(SidebarRowKind.Tag, "v1", Commit);

        var update = Reduce(TagState.Empty, new TagDeleteRequested(Tab, target), refs);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Contains("v1", confirm.Prompt.Lines[0]);
        Assert.Equal($"Undo: git tag v1 {Annotated}", confirm.Prompt.Consequence);
        Assert.Equal(new TagDeleteConfirmed(Tab, "v1", Annotated), confirm.OnAccepted);
    }

    [Fact]
    public void GivenTwoTagsAtTheCommit_WhenDeletingFromTheGraph_ThenNothingIsAsked()
    {
        var refs = Tags(("v1", Commit, Commit), ("v1-rc", Commit, Commit));

        var update = Reduce(TagState.Empty, new TagDeleteRequested(Tab, Graph(Commit)), refs);

        Assert.Equal("2 tags point here: pick one in the sidebar", Warning(update));
    }

    [Fact]
    public void GivenACommitWithoutTag_WhenDeleting_ThenTheWarningSaysToSelectATag()
    {
        var update = Reduce(TagState.Empty, new TagDeleteRequested(Tab, Graph(Commit)));

        Assert.Equal("Select a tag (sidebar) or a commit with one tag on it", Warning(update));
    }

    [Fact]
    public void GivenABranchRow_WhenDeleting_ThenItIsRefusedAsNotATag()
    {
        var target = new TagTarget(SidebarRowKind.LocalBranch, "main", Commit);

        var update = Reduce(TagState.Empty, new TagDeleteRequested(Tab, target), Tags(("main", Commit, Commit)));

        Assert.Equal("Select a tag first", Warning(update));
    }

    [Fact]
    public void GivenTheConfirmAnswered_WhenNothingRuns_ThenTheDeleteIsRequested()
    {
        var update = Reduce(TagState.Empty, new TagDeleteConfirmed(Tab, "v1", Annotated));

        var delete = Assert.IsType<DeleteTagEffect>(Assert.Single(update.Effects));
        Assert.Equal("v1", delete.Name);
    }

    [Fact]
    public void GivenTheConfirmAnswered_WhenAnotherOperationStartedMeanwhile_ThenNothingIsDeleted()
    {
        var update = Reduce(TagState.Empty, new TagDeleteConfirmed(Tab, "v1", Annotated), busy: true);

        Assert.DoesNotContain(update.Effects, effect => effect is TagEffect);
        Assert.Equal($"{TagReducer.BusyNotice}; nothing was deleted", Warning(update));
    }

    [Fact]
    public void GivenATag_WhenPushing_ThenTheRemotesAreRead()
    {
        var refs = Tags(("v1", Commit, Commit));

        var update = Reduce(TagState.Empty, new TagPushRequested(Tab, Graph(Commit)), refs);

        var list = Assert.IsType<ListTagRemotesEffect>(Assert.Single(update.Effects));
        Assert.Equal("v1", list.Name);
        Assert.True(list.Push);
    }

    [Fact]
    public void GivenNoRemote_WhenTheRemotesLoad_ThenNoPromptOpensAndTheWarningSaysSo()
    {
        var update = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", true, []));

        Assert.Null(update.State.Prompt);
        Assert.Equal("There is no remote: add one first", Warning(update));
    }

    [Fact]
    public void GivenTheTabWasLeft_WhenTheRemotesLoad_ThenNoPromptOpens()
    {
        var update = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", true, ["origin"]), active: false);

        Assert.Null(update.State.Prompt);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTwoRemotes_WhenTyping_ThenOnlyMatchingRemotesAreOffered()
    {
        var opened = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", true, ["origin", "backup"]));
        Assert.Equal(2, opened.State.Prompt!.Candidates.Length);

        var typed = Reduce(opened.State, new TagPromptTextChanged(Tab, "bac"));

        Assert.Equal(["backup"], typed.State.Prompt!.Candidates);
    }

    [Fact]
    public void GivenTwoRemotes_WhenMovingUpFromTheFirst_ThenTheHighlightWrapsToTheLast()
    {
        var opened = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", true, ["origin", "backup"]));

        var moved = Reduce(opened.State, new TagPromptMoved(Tab, -1));

        Assert.Equal(1, moved.State.Prompt!.Index);
    }

    [Fact]
    public void GivenAChosenRemote_WhenAcceptingAPush_ThenOnlyThatTagGoesToThatRemote()
    {
        var opened = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", true, ["origin", "backup"]));
        var moved = Reduce(opened.State, new TagPromptMoved(Tab, 1));

        var update = Reduce(moved.State, new TagPromptAccepted(Tab));

        var push = Assert.Single(update.Effects.OfType<PushTagEffect>());
        Assert.Equal(("backup", "v1"), (push.Remote, push.Name));
        Assert.Null(update.State.Prompt);
    }

    [Fact]
    public void GivenNoMatchingRemote_WhenAccepting_ThenThePromptStaysOpenWithAWarning()
    {
        var opened = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", true, ["origin"]));
        var typed = Reduce(opened.State, new TagPromptTextChanged(Tab, "zzz"));

        var update = Reduce(typed.State, new TagPromptAccepted(Tab));

        Assert.NotNull(update.State.Prompt);
        Assert.Equal("No matching remote", Warning(update));
        Assert.DoesNotContain(update.Effects, effect => effect is TagEffect);
    }

    [Fact]
    public void GivenARemote_WhenAcceptingARemoteDelete_ThenTheConfirmNamesRemoteAndTagAndTheLocalTagStays()
    {
        var opened = Reduce(TagState.Empty, new TagRemotesLoaded(Tab, "v1", false, ["origin"]));

        var update = Reduce(opened.State, new TagPromptAccepted(Tab));

        var confirm = Assert.Single(update.Effects.OfType<ShowConfirmEffect>());
        Assert.Contains("v1", confirm.Prompt.Lines[0]);
        Assert.Contains("origin", confirm.Prompt.Lines[0]);
        Assert.Equal("The local tag stays.", confirm.Prompt.Lines[1]);
        Assert.Equal(new TagDeleteOnRemoteConfirmed(Tab, "origin", "v1"), confirm.OnAccepted);
    }

    [Fact]
    public void GivenASuccess_WhenReducing_ThenTheTabIsRefreshedAndTheTextIsPosted()
    {
        var update = Reduce(TagState.Empty, new TagOperationSucceeded(Tab, "Created tag v1"));

        Assert.Contains(
            update.Effects,
            effect => effect is PostStatusEffect { Text: "Created tag v1", Severity: StatusSeverity.Info });
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenAFailureThatNeedsCredentials_WhenReducing_ThenTheTerminalRetryIsOffered()
    {
        var retry = new TagRetry("Tag push", ["push", "origin", "refs/tags/v1"]);

        var update = Reduce(TagState.Empty, new TagOperationFailed(Tab, "denied", retry));

        Assert.Contains(update.Effects, effect => effect is PostStatusEffect { Severity: StatusSeverity.Error });
        var offer = Assert.Single(update.Effects.OfType<OfferTerminalRetryEffect>());
        Assert.Equal("Tag push", offer.Noun);
    }

    [Fact]
    public void GivenAFailureWithoutCredentials_WhenReducing_ThenNoRetryIsOfferedButTheTabIsRefreshed()
    {
        var update = Reduce(TagState.Empty, new TagOperationFailed(Tab, "rejected", null));

        Assert.DoesNotContain(update.Effects, effect => effect is OfferTerminalRetryEffect);
        Assert.Contains(update.Effects, effect => effect is RefreshTabEffect);
    }

    [Fact]
    public void GivenTheRemotesFailed_WhenReducing_ThenTheErrorIsPostedWithoutRefreshing()
    {
        var update = Reduce(TagState.Empty, new TagRemotesFailed(Tab, "no remotes"));

        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Severity);
    }

    [Fact]
    public void GivenACancelledOperation_WhenReducing_ThenNothingIsPosted()
    {
        var update = Reduce(TagState.Empty, new TagOperationCancelled(Tab));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenEveryConcreteTagCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<TagCommand>();
        Assert.NotEmpty(commands);
        var prompt = Typed(TagPromptKind.CreateName, "x").Prompt;
        var state = new TagState(prompt);

        foreach (var command in commands)
        {
            try
            {
                TagReducer.Reduce(state, command, Context([], true));
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

    private static TagUpdate Reduce(
        TagState state,
        TagCommand command,
        ImmutableArray<GitRef>? refs = null,
        bool active = true,
        bool busy = false) =>
        TagReducer.Reduce(state, command, Context(refs ?? [], active, busy));

    private static TagContext Context(ImmutableArray<GitRef> refs, bool active, bool busy = false) =>
        new(Tab, Location(), refs, active, busy);

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-tag-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static TagTarget Graph(string commit) => new(null, null, commit);

    private static TagState Typed(TagPromptKind kind, string text, string newName = "")
    {
        var opened = TagReducer.Reduce(
            TagState.Empty,
            new TagCreateRequested(Tab, Graph(Commit)),
            Context([], true)).State;
        return new TagState(opened.Prompt! with { Kind = kind, Text = text, NewName = newName });
    }

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

    private static string Warning(TagUpdate update)
    {
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects.OfType<PostStatusEffect>()));
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        return post.Text;
    }
}
