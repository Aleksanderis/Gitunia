using Gitunia.Core.Effects;
using Gitunia.Core.Ignore;
using Gitunia.Core.Prompts;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Ignore;

// The ignore reducer inside the root reducer: the prompt, the running mark and a result for a closed tab.
public class IgnoreAppReducerTests
{
    [Fact]
    public void GivenThePromptAnswered_WhenReducing_ThenTheTabIsBusyUntilTheResultArrives()
    {
        var (state, tabId) = Opened(Untracked("logs/debug.log"), Modified("a.txt"));
        var asked = OpenPrompt(state, tabId);

        var started = AppReducer.Reduce(asked, new PromptChoiceSelectedCommand(1));

        var effect = Assert.IsType<AddIgnorePatternEffect>(Assert.Single(started.Effects));
        Assert.Equal("*.log", effect.Pattern);
        Assert.True(Running(started.State, tabId).IsRunning(RunningOperationKind.Ignore));
        var busy = AppReducer.Reduce(started.State, new IgnoreRequested(tabId));
        Assert.Equal(IgnoreReducer.BusyNotice, Assert.IsType<PostStatusEffect>(Assert.Single(busy.Effects)).Text);
        var finished = new IgnoreSucceeded(tabId, "Added *.log to .gitignore", StatusSeverity.Info);
        var done = AppReducer.Reduce(started.State, finished);
        Assert.False(Running(done.State, tabId).IsBusy);
    }

    [Fact]
    public void GivenAStagingRuns_WhenAsking_ThenTheIgnoreIsNotBlocked()
    {
        var (state, tabId) = Opened(Untracked("a.log"));
        var tab = state.Workspace.FindTab(tabId)!;
        var running = new RunningOperations([new RunningOperation(1, RunningOperationKind.Staging)], 1);
        var tabs = state.Workspace.Tabs.SetItem(0, tab with { Running = running });
        var busy = state with { Workspace = state.Workspace with { Tabs = tabs } };

        var update = AppReducer.Reduce(busy, new IgnoreRequested(tabId));

        Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenAResultForAClosedTab_WhenReducing_ThenItIsDropped()
    {
        var (state, _) = Opened(Untracked("a.log"));

        var update = AppReducer.Reduce(state, new IgnoreSucceeded(new TabId(99), "Added", StatusSeverity.Info));

        Assert.Empty(update.Effects);
    }

    private static AppState OpenPrompt(AppState state, TabId tabId)
    {
        var asked = AppReducer.Reduce(state, new IgnoreRequested(tabId));
        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(asked.Effects));

        return AppReducer.Reduce(asked.State, new PromptRequestedCommand(prompt.Request)).State;
    }

    private static RunningOperations Running(AppState state, TabId tabId) => state.Workspace.FindTab(tabId)!.Running;

    private static (AppState State, TabId TabId) Opened(params StatusEntry[] entries)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-ignore-app-tests", "alpha");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var state = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
        var tabId = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new WorkingTreeLoadedCommand(Snapshot(entries)))).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new OpenWorkingTreeCommand())).State;
        state = AppReducer.Reduce(state, new TabCommand(tabId, new FocusPaneCommand(PaneKind.WorkingUnstaged))).State;

        return (state, tabId);
    }
}
