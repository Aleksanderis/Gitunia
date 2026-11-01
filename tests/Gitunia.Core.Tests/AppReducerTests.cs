using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests;

public class AppReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly ConfirmPrompt DeletePrompt =
        new("Delete tag", ["Delete tag v1?"], "Undo: recreate the tag", "Delete tag");

    private const string Commit = "1111111111111111111111111111111111111111";

    [Fact]
    public void GivenAWorkspaceCommand_WhenReducing_ThenTheWorkspaceChangesAndTheLoadEffectComesOut()
    {
        var update = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Location("alpha")));

        var tab = Assert.Single(update.State.Workspace.Tabs);
        var load = Assert.IsType<LoadRepositoryEffect>(Assert.Single(update.Effects));
        Assert.Equal(tab.Id, load.TabId);
    }

    [Fact]
    public void GivenATagCommand_WhenReducing_ThenOnlyThatTabsTagSliceChanges()
    {
        var state = Opened("alpha", "beta");
        var first = state.Workspace.Tabs[0].Id;
        var second = state.Workspace.Tabs[1].Id;

        var update = AppReducer.Reduce(state, new TagCreateRequested(second, new TagTarget(null, null, Commit)));

        Assert.NotNull(update.State.Workspace.FindTab(second)!.Tags.Prompt);
        Assert.Null(update.State.Workspace.FindTab(first)!.Tags.Prompt);
        Assert.Equal(state.Workspace.ActiveTabId, update.State.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenAClosedTab_WhenATagResultArrives_ThenItIsDroppedWithoutEffects()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new TagOperationSucceeded(new TabId(99), "Created tag v1"));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnEffectWithoutFeatureSlice_WhenTheRunnerReportsAnEscapedException_ThenTheErrorIsPosted()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new EffectFailedCommand("Internal error: boom"));

        Assert.Same(state, update.State);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.Equal("Internal error: boom", post.Text);
    }

    [Fact]
    public void GivenATabWithAnOpenTagPrompt_WhenItIsClosed_ThenTheKeyModeGoesBackToNormal()
    {
        var state = Opened("alpha");
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TagCreateRequested(tabId, new TagTarget(null, null, Commit))).State;

        var update = AppReducer.Reduce(state, new CloseTabCommand(tabId));

        Assert.Contains(update.Effects, effect => effect is SetKeyModeEffect { Mode: KeyMode.Normal });
    }

    [Fact]
    public void GivenATabWithoutPrompt_WhenItIsClosed_ThenNoKeyModeEffectComesOut()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new CloseTabCommand(state.Workspace.Tabs[0].Id));

        Assert.DoesNotContain(update.Effects, effect => effect is SetKeyModeEffect);
    }

    [Fact]
    public void GivenATagDeleteConfirmed_WhenReducing_ThenOnlyThatTabIsMarkedRunningUntilTheResult()
    {
        var state = Opened("alpha", "beta");
        var first = state.Workspace.Tabs[0].Id;
        var second = state.Workspace.Tabs[1].Id;

        var started = AppReducer.Reduce(state, new TagDeleteConfirmed(first, "v1", Commit)).State;
        var finished = AppReducer.Reduce(started, new TagOperationSucceeded(first, "Deleted tag v1")).State;

        Assert.True(started.Workspace.FindTab(first)!.Running.IsBusy);
        Assert.False(started.Workspace.FindTab(second)!.Running.IsBusy);
        Assert.False(finished.Workspace.FindTab(first)!.Running.IsBusy);
    }

    [Fact]
    public void GivenARunningTagOperation_WhenAnotherIsConfirmed_ThenItIsRefusedFromStateAndNothingStarts()
    {
        var state = Opened("alpha");
        var tabId = state.Workspace.Tabs[0].Id;
        var started = AppReducer.Reduce(state, new TagDeleteConfirmed(tabId, "v1", Commit)).State;

        var update = AppReducer.Reduce(started, new TagDeleteConfirmed(tabId, "v2", Commit));

        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Contains(TagReducer.BusyNotice, post.Text, StringComparison.Ordinal);
        Assert.Single(update.State.Workspace.FindTab(tabId)!.Running.Items);
    }

    [Fact]
    public void GivenARunningTagOperation_WhenItFailsOrIsCancelled_ThenTheTabIsFreeForTheNextOne()
    {
        var state = Opened("alpha");
        var tabId = state.Workspace.Tabs[0].Id;
        var started = AppReducer.Reduce(state, new TagDeleteConfirmed(tabId, "v1", Commit)).State;

        var failed = AppReducer.Reduce(started, new TagOperationFailed(tabId, "boom", null)).State;
        var again = AppReducer.Reduce(failed, new TagDeleteConfirmed(tabId, "v1", Commit));

        Assert.False(failed.Workspace.FindTab(tabId)!.Running.IsBusy);
        Assert.Contains(again.Effects, effect => effect is DeleteTagEffect);
    }

    [Fact]
    public void GivenAConfirmedTagDelete_WhenYIsAnswered_ThenTheAcceptedCommandRunsAndTheDialogCloses()
    {
        var (state, tabId) = AskedToDeleteTag();

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Contains(update.Effects, effect => effect is DeleteTagEffect);
        Assert.True(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenAnOpenTagDeleteDialog_WhenNIsAnswered_ThenNothingStarts()
    {
        var (state, tabId) = AskedToDeleteTag();

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Empty(update.Effects);
        Assert.False(update.State.Workspace.FindTab(tabId)!.Running.IsBusy);
    }

    [Fact]
    public void GivenAnOpenTagDeleteDialog_WhenTheTabIsClosedAndYIsAnswered_ThenNothingStartsAndTheDialogCloses()
    {
        var (state, tabId) = AskedToDeleteTag();
        state = AppReducer.Reduce(state, new CloseTabCommand(tabId)).State;
        Assert.NotNull(state.Shell.Confirm);

        var update = AppReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenATooSmallScreen_WhenATagDeleteIsAsked_ThenNoDialogOpensAndTheRefusalIsPosted()
    {
        var state = Opened("alpha");
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new ScreenResizedCommand(new ScreenExtent(80, 5), Start)).State;
        var refusal = new StatusPostedCommand("The screen is too small to ask", StatusSeverity.Warning, Start);

        var update = AppReducer.Reduce(state, new ConfirmRequestedCommand(DeletePrompt, DeleteAccepted(tabId), refusal));

        Assert.Null(update.State.Shell.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal("The screen is too small to ask", update.State.Shell.Status.Current(Start)?.Text);
        Assert.DoesNotContain(update.Effects, effect => effect is OperationEffect);
    }

    [Fact]
    public void GivenAShellCommand_WhenReducing_ThenTheWorkspaceIsUntouched()
    {
        var state = Opened("alpha");

        var update = AppReducer.Reduce(state, new SetKeyModeCommand(KeyMode.Insert));

        Assert.Same(state.Workspace, update.State.Workspace);
        Assert.Equal(KeyMode.Insert, update.State.Shell.Mode);
    }

    private static (AppState State, TabId TabId) AskedToDeleteTag()
    {
        var state = Opened("alpha");
        var tabId = state.Workspace.Tabs[0].Id;
        var asked = AppReducer.Reduce(state, new ConfirmRequestedCommand(DeletePrompt, DeleteAccepted(tabId), null));

        return (asked.State, tabId);
    }

    private static TagDeleteConfirmed DeleteAccepted(TabId tabId) => new(tabId, "v1", Commit);

    private static AppState Opened(params string[] names)
    {
        var state = AppState.Empty;
        foreach (var name in names)
        {
            state = AppReducer.Reduce(state, new OpenRepositoryCommand(Location(name))).State;
        }

        return state;
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-app-reducer-tests", name);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
