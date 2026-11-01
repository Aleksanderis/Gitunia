using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Remotes;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Remotes;

public class RemoteReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RemoteTarget Nothing = new(null, null);
    private static readonly ImmutableArray<RemoteInfo> Origin = [new RemoteInfo("origin", "https://example.com/o.git")];

    [Fact]
    public void GivenAnAction_WhenRequested_ThenTheRemotesAreReadFirst()
    {
        var effects = Reduce(new RemoteActionRequested(Tab, RemoteAction.Rename, Nothing));

        var list = Assert.IsType<ListRemotesEffect>(Assert.Single(effects));
        Assert.Equal(RemoteAction.Rename, list.Action);
        Assert.Equal(Tab, list.TabId);
    }

    [Fact]
    public void GivenARunningOperation_WhenRequested_ThenItIsRefusedAsBusy()
    {
        var effects = Reduce(new RemoteActionRequested(Tab, RemoteAction.Add, Nothing), busy: true);

        Assert.Equal(RemoteReducer.BusyNotice, Warning(effects));
        Assert.DoesNotContain(effects, effect => effect is RemoteEffect);
    }

    [Fact]
    public void GivenAFetchRuns_WhenManagingRemotes_ThenItIsRefusedWithoutReadingAnything()
    {
        var effects = Reduce(new RemoteActionRequested(Tab, RemoteAction.Remove, Nothing), syncing: true);

        Assert.Equal(RemoteReducer.SyncRunsNotice, Warning(effects));
        Assert.DoesNotContain(effects, effect => effect is RemoteEffect);
    }

    [Fact]
    public void GivenAFetchStartedWhileTheRemoveDialogWasOpen_WhenConfirmed_ThenTheRemoteStays()
    {
        var effects = Reduce(new RemoteRemoveConfirmed(Tab, "origin", "https://example.com/o.git"), syncing: true);

        Assert.StartsWith(RemoteReducer.SyncRunsNotice, Warning(effects), StringComparison.Ordinal);
        Assert.EndsWith("nothing was removed", Warning(effects), StringComparison.Ordinal);
        Assert.DoesNotContain(effects, effect => effect is RemoteEffect);
    }

    [Fact]
    public void GivenTheRemotesRead_WhenAdding_ThenTheNamePromptOpensForTheTab()
    {
        var effects = Reduce(new RemotesListed(Tab, RemoteAction.Add, Nothing, Origin));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Add remote", request.Title);
        Assert.Equal("Name of the new remote", request.Context);
        Assert.Equal("next", request.AcceptLabel);
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(new RemoteAddNameAnswer(Tab, Origin), request.OnAccepted);
    }

    [Fact]
    public void GivenAnotherTabIsActive_WhenTheRemotesAreRead_ThenNoPromptOpens()
    {
        var effects = Reduce(new RemotesListed(Tab, RemoteAction.Add, Nothing, Origin), active: false);

        Assert.Empty(effects);
    }

    [Fact]
    public void GivenNoRemote_WhenRemoving_ThenItSaysSoWithoutAPrompt()
    {
        var effects = Reduce(new RemotesListed(Tab, RemoteAction.Remove, Nothing, []));

        Assert.Equal("There is no remote: add one first", Warning(effects));
    }

    [Theory]
    [InlineData(RemoteAction.Remove, "Remove remote", "Remote to remove")]
    [InlineData(RemoteAction.Rename, "Rename remote", "Remote to rename")]
    [InlineData(RemoteAction.SetUrl, "Set remote URL", "Remote to change")]
    public void GivenRemotesAndNoSelection_WhenRead_ThenThePickerListsThem(
        RemoteAction action,
        string title,
        string context)
    {
        var effects = Reduce(new RemotesListed(Tab, action, Nothing, Origin));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(title, request.Title);
        Assert.Equal(context, request.Context);
        Assert.Equal(RemoteReducer.NoRemoteMatch, request.NoMatch);
        Assert.Equal("origin", request.Choices!.Value.Single().Value);
        Assert.Equal(new RemotePickAnswer(Tab, action, Origin), request.OnAccepted);
    }

    [Fact]
    public void GivenARemoteBranchRowSelected_WhenRemoving_ThenItsRemoteIsConfirmedWithoutAPicker()
    {
        ImmutableArray<RemoteInfo> remotes = [new("origin", "u1"), new("other", "u2")];
        var target = new RemoteTarget(SidebarRowKind.RemoteBranch, "origin/main");
        var refs = Branches("origin/main", "origin/topic", "other/x");

        var effects = Reduce(new RemotesListed(Tab, RemoteAction.Remove, target, remotes), refs: refs);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Remove the remote origin (u1)?", confirm.Prompt.Lines[0]);
        Assert.Equal("2 remote-tracking branches disappear: origin/main, origin/topic.", confirm.Prompt.Lines[1]);
        Assert.Equal("Undo: git remote add origin u1, then fetch it", confirm.Prompt.Consequence);
        Assert.Equal(new RemoteRemoveConfirmed(Tab, "origin", "u1"), confirm.OnAccepted);
    }

    [Fact]
    public void GivenOneRemoteNameExtendsAnother_WhenRemoving_ThenOnlyTheOwnBranchesAreListed()
    {
        ImmutableArray<RemoteInfo> remotes = [new("a", "u1"), new("a/b", "u2")];
        var refs = Branches("a/main", "a/b/main");

        var effects = Reduce(new RemoteChosen(Tab, RemoteAction.Remove, remotes, "a"), refs: refs);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("1 remote-tracking branch disappears: a/main.", confirm.Prompt.Lines[1]);
    }

    [Fact]
    public void GivenTheLongerRemoteNameSelected_WhenRemoving_ThenTheLongestMatchWins()
    {
        ImmutableArray<RemoteInfo> remotes = [new("a", "u1"), new("a/b", "u2")];
        var target = new RemoteTarget(SidebarRowKind.RemoteBranch, "a/b/main");

        var effects = Reduce(new RemotesListed(Tab, RemoteAction.Remove, target, remotes));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal(new RemoteRemoveConfirmed(Tab, "a/b", "u2"), confirm.OnAccepted);
    }

    [Fact]
    public void GivenNoTrackingBranches_WhenRemoving_ThenTheDialogSaysSo()
    {
        var effects = Reduce(new RemoteChosen(Tab, RemoteAction.Remove, Origin, "origin"));

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("It has no remote-tracking branches.", confirm.Prompt.Lines[1]);
    }

    [Fact]
    public void GivenManyTrackingBranches_WhenRemoving_ThenTheDialogListsFiveAndCountsTheRest()
    {
        var refs = Branches("origin/a", "origin/b", "origin/c", "origin/d", "origin/e", "origin/f", "origin/g");

        var effects = Reduce(new RemoteChosen(Tab, RemoteAction.Remove, Origin, "origin"), refs: refs);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal(
            "7 remote-tracking branches disappear: origin/a, origin/b, origin/c, origin/d, origin/e and 2 more.",
            confirm.Prompt.Lines[1]);
    }

    [Fact]
    public void GivenAChosenRemote_WhenRenaming_ThenTheNewNamePromptOpens()
    {
        var effects = Reduce(new RemoteChosen(Tab, RemoteAction.Rename, Origin, "origin"));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("New name for origin", request.Context);
        Assert.Equal("", request.InitialText);
        Assert.Equal(new RemoteRenameAnswer(Tab, Origin, "origin"), request.OnAccepted);
    }

    [Fact]
    public void GivenAChosenRemote_WhenSettingTheUrl_ThenThePromptStartsWithTheCurrentUrl()
    {
        var effects = Reduce(new RemoteChosen(Tab, RemoteAction.SetUrl, Origin, "origin"));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("New URL of origin", request.Context);
        Assert.Equal("https://example.com/o.git", request.InitialText);
    }

    [Fact]
    public void GivenAnUnknownChoice_WhenChosen_ThenNothingHappens()
    {
        Assert.Empty(Reduce(new RemoteChosen(Tab, RemoteAction.Remove, Origin, "gone")));
    }

    [Fact]
    public void GivenAnExistingName_WhenAddingTheName_ThenItIsRefusedBeforeAskingForTheUrl()
    {
        var effects = Reduce(new RemoteAddNameAccepted(Tab, Origin, " origin "));

        Assert.Equal("Remote origin already exists", Warning(effects));
    }

    [Fact]
    public void GivenAnEmptyName_WhenAddingTheName_ThenNothingIsAdded()
    {
        Assert.Equal("No name: nothing was added", Warning(Reduce(new RemoteAddNameAccepted(Tab, Origin, "  "))));
    }

    [Fact]
    public void GivenANewName_WhenAddingTheName_ThenTheUrlPromptNamesIt()
    {
        var effects = Reduce(new RemoteAddNameAccepted(Tab, Origin, " backup "));

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("URL of backup", request.Context);
        Assert.Equal(new RemoteAddUrlAnswer(Tab, "backup"), request.OnAccepted);
    }

    [Fact]
    public void GivenAUrl_WhenAddingTheUrl_ThenTheRemoteIsAdded()
    {
        var effects = Reduce(new RemoteAddUrlAccepted(Tab, "backup", " u "));

        var add = Assert.IsType<AddRemoteEffect>(Assert.Single(effects));
        Assert.Equal(("backup", "u"), (add.Name, add.Url));
    }

    [Fact]
    public void GivenNoUrl_WhenAddingTheUrl_ThenNothingIsAdded()
    {
        var effects = Reduce(new RemoteAddUrlAccepted(Tab, "backup", " "));

        Assert.Equal("No URL: nothing was added", Warning(effects));
    }

    [Fact]
    public void GivenARunningOperation_WhenAddingTheUrl_ThenNothingIsAdded()
    {
        var effects = Reduce(new RemoteAddUrlAccepted(Tab, "backup", "u"), busy: true);

        Assert.Equal($"{RemoteReducer.BusyNotice}; nothing was added", Warning(effects));
    }

    [Fact]
    public void GivenANewName_WhenRenaming_ThenTheRemoteIsRenamed()
    {
        var effects = Reduce(new RemoteRenameAccepted(Tab, Origin, "origin", " upstream "));

        var rename = Assert.IsType<RenameRemoteEffect>(Assert.Single(effects));
        Assert.Equal(("origin", "upstream"), (rename.OldName, rename.NewName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("origin")]
    public void GivenTheSameOrEmptyName_WhenRenaming_ThenNothingRuns(string name)
    {
        var effects = Reduce(new RemoteRenameAccepted(Tab, Origin, "origin", name));

        Assert.Equal("Same or empty name: nothing was renamed", Warning(effects));
    }

    [Fact]
    public void GivenTheNameOfAnotherRemote_WhenRenaming_ThenItIsRefused()
    {
        ImmutableArray<RemoteInfo> remotes = [new("origin", "u"), new("other", "u2")];

        var effects = Reduce(new RemoteRenameAccepted(Tab, remotes, "origin", "other"));

        Assert.Equal("Remote other already exists", Warning(effects));
    }

    [Fact]
    public void GivenANewUrl_WhenSettingIt_ThenTheUrlIsChanged()
    {
        var effects = Reduce(new RemoteUrlAccepted(Tab, "origin", "old", " new "));

        var set = Assert.IsType<SetRemoteUrlEffect>(Assert.Single(effects));
        Assert.Equal(("origin", "old", "new"), (set.Name, set.OldUrl, set.Url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("old")]
    public void GivenTheSameOrEmptyUrl_WhenSettingIt_ThenNothingRuns(string url)
    {
        var effects = Reduce(new RemoteUrlAccepted(Tab, "origin", "old", url));

        Assert.Equal("Same or empty URL: nothing was changed", Warning(effects));
    }

    [Fact]
    public void GivenTheRemoveConfirmed_WhenReducing_ThenTheRemoteIsRemoved()
    {
        var effects = Reduce(new RemoteRemoveConfirmed(Tab, "origin", "u"));

        var remove = Assert.IsType<RemoveRemoteEffect>(Assert.Single(effects));
        Assert.Equal(("origin", "u"), (remove.Name, remove.Url));
    }

    [Fact]
    public void GivenARunningOperation_WhenTheRemoveIsConfirmed_ThenNothingIsRemoved()
    {
        var effects = Reduce(new RemoteRemoveConfirmed(Tab, "origin", "u"), busy: true);

        Assert.Equal($"{RemoteReducer.BusyNotice}; nothing was removed", Warning(effects));
    }

    [Fact]
    public void GivenASuccess_WhenReducing_ThenItIsPostedAndTheTabRefreshed()
    {
        var effects = Reduce(new RemoteOperationSucceeded(Tab, "Done"));

        Assert.Equal(StatusSeverity.Info, effects.OfType<PostStatusEffect>().Single().Severity);
        Assert.Equal(Tab, effects.OfType<RefreshTabEffect>().Single().TabId);
    }

    [Fact]
    public void GivenAFailure_WhenReducing_ThenTheErrorIsPostedAndTheTabStillRefreshed()
    {
        var effects = Reduce(new RemoteOperationFailed(Tab, "Could not add the remote: no"));

        Assert.Equal(StatusSeverity.Error, effects.OfType<PostStatusEffect>().Single().Severity);
        Assert.Single(effects.OfType<RefreshTabEffect>());
    }

    [Fact]
    public void GivenTheRemotesCouldNotBeRead_WhenReducing_ThenTheErrorIsPostedWithoutRefreshing()
    {
        var effects = Reduce(new RemotesListFailed(Tab, "Could not list the remotes: x"));

        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(Assert.Single(effects)).Severity);
    }

    [Fact]
    public void GivenACancelledOperation_WhenReducing_ThenNothingIsPosted()
    {
        Assert.Empty(Reduce(new RemoteOperationCancelled(Tab)));
    }

    [Fact]
    public void GivenEveryConcreteRemoteCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<RemoteCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                RemoteReducer.Reduce(command, Context([], true, false));
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in RemoteReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static ImmutableArray<AppEffect> Reduce(
        RemoteCommand command,
        ImmutableArray<GitRef>? refs = null,
        bool active = true,
        bool busy = false,
        bool syncing = false) =>
        RemoteReducer.Reduce(command, Context(refs ?? [], active, busy, syncing));

    private static RemoteContext Context(ImmutableArray<GitRef> refs, bool active, bool busy, bool syncing = false) =>
        new(Tab, Location(), refs, active, busy, syncing);

    private static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-remote-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private static ImmutableArray<GitRef> Branches(params string[] names) =>
    [
        .. names.Select(name => new GitRef(
            $"refs/remotes/{name}",
            name,
            RefKind.RemoteBranch,
            "1111111111111111111111111111111111111111",
            "1111111111111111111111111111111111111111",
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
