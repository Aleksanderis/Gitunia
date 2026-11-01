using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Remotes;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Remotes;

// The remote reducer inside the root reducer: running marks, dropped late commands and the open dialog.
public class RemoteAppReducerTests
{
    private static readonly ConfirmPrompt RemovePrompt =
        new("Remove remote", ["Remove the remote origin (u)?"], "Undo: git remote add origin u", "Remove remote");

    [Fact]
    public void GivenARequest_WhenReducing_ThenTheTabIsMarkedBusyUntilTheResultArrives()
    {
        var (state, tabId) = Opened();
        var target = new RemoteTarget(null, null);

        var asked = AppReducer.Reduce(state, new RemoteActionRequested(tabId, RemoteAction.Add, target));
        Assert.IsType<ListRemotesEffect>(Assert.Single(asked.Effects));
        Assert.True(asked.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Remotes));

        var busy = AppReducer.Reduce(asked.State, new RemoteActionRequested(tabId, RemoteAction.Remove, target));
        Assert.IsType<PostStatusEffect>(Assert.Single(busy.Effects));

        var listed = AppReducer.Reduce(asked.State, new RemotesListed(tabId, RemoteAction.Add, target, []));
        Assert.False(listed.State.Workspace.FindTab(tabId)!.Running.IsBusy);
        Assert.IsType<ShowPromptEffect>(Assert.Single(listed.Effects));
    }

    [Fact]
    public void GivenAClosedTab_WhenARemoteResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var (state, _) = Opened();

        var update = AppReducer.Reduce(state, new RemoteOperationSucceeded(new TabId(99), "Added remote x"));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenRemoveDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingStartsAndTheDialogCloses()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(RemovePrompt, new RemoteRemoveConfirmed(tabId, "origin", "u"), null));
        state = AppReducer.Reduce(asked.State, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenRemoveDialog_WhenYIsAnswered_ThenTheRemoveStartsAndTheTabIsBusy()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(RemovePrompt, new RemoteRemoveConfirmed(tabId, "origin", "u"), null));

        var update = AppReducer.Reduce(asked.State, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.IsType<RemoveRemoteEffect>(Assert.Single(update.Effects));
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsRunning(RunningOperationKind.Remotes));
    }

    [Fact]
    public void GivenAnOpenRemoveDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = Opened();
        var asked = AppReducer.Reduce(
            state,
            new ConfirmRequestedCommand(RemovePrompt, new RemoteRemoveConfirmed(tabId, "origin", "u"), null));

        var update = AppReducer.Reduce(asked.State, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Empty(update.Effects);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenAnOpenRemotePrompt_WhenTheTabIsClosedAndTheAnswerArrivesLate_ThenItIsDropped()
    {
        var (state, tabId) = Opened();
        var listed = AppReducer.Reduce(state, new RemotesListed(tabId, RemoteAction.Add, new RemoteTarget(null, null), []));
        state = AppReducer.Reduce(listed.State, new PromptRequestedCommand(((ShowPromptEffect)listed.Effects[0]).Request)).State;
        Assert.NotNull(state.Shell.Prompt);
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.Null(state.Shell.Prompt);

        var late = AppReducer.Reduce(state, new RemoteAddNameAccepted(tabId, [new RemoteInfo("a", "b")], "x"));

        Assert.Empty(late.Effects);
    }

    private static (AppState State, TabId TabId) Opened()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-remote-app-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;

        return (state, state.Workspace.Tabs[0].Id);
    }
}
