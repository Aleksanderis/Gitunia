using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests;

public class AppReducerTests
{
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
