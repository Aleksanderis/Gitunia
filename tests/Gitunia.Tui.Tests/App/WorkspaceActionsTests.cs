using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Commits;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App;

// The view side of the tab and picker actions: it asks the reducer who owns an action and dispatches the rest; the
// decisions themselves are tested in Gitunia.Core.Tests (WorkspaceActionReducerTests).
public class WorkspaceActionsTests
{
    [Fact]
    public void GivenThreeTabs_WhenATabClickRunsTabNextWithItsNumber_ThenThatTabIsActive()
    {
        var setup = Create(3);

        var handled = setup.Actions.TryExecute(ActionIds.TabNext, 3);

        Assert.True(handled);
        Assert.Equal(setup.Host.Workspace.Tabs[2].Id, setup.Host.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenATabIsClicked_ThenThePickerClosesAndTheTabIsActive()
    {
        var setup = Create(2);
        OpenPicker(setup.Host);

        setup.Actions.TryExecute(ActionIds.TabNext, 1);

        Assert.False(setup.Host.State.Shell.Picker.IsOpen);
        Assert.Equal(setup.Host.Workspace.Tabs[0].Id, setup.Host.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenNoTabs_WhenATabClickArrives_ThenNothingHappens()
    {
        var setup = Create(0);

        var handled = setup.Actions.TryExecute(ActionIds.TabNext, 1);

        Assert.True(handled);
        Assert.Empty(setup.Host.Workspace.Tabs);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenTheMouseDoubleClickActionRuns_ThenThePickerHandlesIt()
    {
        var setup = Create(0);
        OpenPicker(setup.Host);

        var handled = setup.Actions.TryExecute(ActionIds.PickerAccept, null);

        Assert.True(handled);
        Assert.StartsWith("nothing to open", setup.Host.State.Shell.Picker.State!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAClosedPicker_WhenAPickerActionArrives_ThenItIsLeftToTheOthers()
    {
        var setup = Create(1);

        Assert.False(setup.Actions.TryExecute(ActionIds.PickerAccept, null));
        Assert.False(setup.Actions.TryExecute(ActionIds.GraphJumpToHead, null));
    }

    [Fact]
    public void GivenAnOpenPickerWithAQuery_WhenPickerOpenRunsAgain_ThenTheStateIsKept()
    {
        var setup = Create(1);
        OpenPicker(setup.Host);
        setup.Host.Dispatch(new TypeTextCommand("abc"));

        setup.Actions.TryExecute(ActionIds.PickerOpen, null);

        Assert.Equal("abc", setup.Host.State.Shell.Picker.State!.Query);
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenClosingTheTab_ThenTheFirstCloseOnlyWarnsAndTheSecondCloses()
    {
        var setup = Create(2, withUnsentMessage: true);

        setup.Actions.TryExecute(ActionIds.TabClose, null);

        Assert.Equal(2, setup.Host.Workspace.Tabs.Length);
        Assert.Contains("Unsent commit message", setup.Actions.Message, StringComparison.Ordinal);

        setup.Actions.TryExecute(ActionIds.TabClose, null);

        Assert.Single(setup.Host.Workspace.Tabs);
        Assert.Empty(setup.Actions.Message);
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenAnotherActionComesBetweenTheCloses_ThenTheGuardWarnsAgain()
    {
        var setup = Create(2, withUnsentMessage: true);
        setup.Actions.TryExecute(ActionIds.TabClose, null);

        setup.Actions.TryExecute(ActionIds.TabLastActive, null);
        setup.Actions.TryExecute(ActionIds.TabClose, null);

        Assert.Equal(2, setup.Host.Workspace.Tabs.Length);
    }

    [Fact]
    public void GivenAWarning_WhenAnActionOfAnotherHandlerComes_ThenTheNoticeIsGone()
    {
        var setup = Create(2, withUnsentMessage: true);
        setup.Actions.TryExecute(ActionIds.TabClose, null);

        var handled = setup.Actions.TryExecute(ActionIds.GraphJumpToHead, null);

        Assert.False(handled);
        Assert.Empty(setup.Actions.Message);
    }

    [Fact]
    public void GivenNoUnsentMessage_WhenClosingTheTab_ThenItClosesAtOnce()
    {
        var setup = Create(2);

        setup.Actions.TryExecute(ActionIds.TabClose, null);

        Assert.Single(setup.Host.Workspace.Tabs);
    }

    [Fact]
    public void GivenATab_WhenRenamingAndAccepting_ThenTheTypedNameIsTheTabTitle()
    {
        var setup = Create(1);
        var prompt = new PromptInputHandler(setup.Host);
        var tab = setup.Host.Workspace.ActiveTab!;

        Assert.True(setup.Actions.TryExecute(ActionIds.TabRename, null));
        Assert.Equal(KeyMode.Insert, setup.Host.State.Shell.Mode);
        Assert.Contains("repo0_", PopupText.Of(prompt), StringComparison.Ordinal);
        Type(prompt, "-2");
        prompt.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal("repo0-2", setup.Host.Workspace.FindTab(tab.Id)!.Title);
        Assert.Equal("repo0", setup.Host.Workspace.FindTab(tab.Id)!.DisplayName);
        Assert.Equal(KeyMode.Normal, setup.Host.State.Shell.Mode);
        Assert.Equal(string.Empty, PopupText.Of(prompt));
    }

    [Fact]
    public void GivenARenamedTab_WhenAcceptingAnEmptyName_ThenTheRepositoryNameIsBack()
    {
        var setup = Create(1);
        var prompt = new PromptInputHandler(setup.Host);
        setup.Actions.TryExecute(ActionIds.TabRename, null);
        Type(prompt, "x");
        prompt.TryExecute(ActionIds.InputAccept, null);

        setup.Actions.TryExecute(ActionIds.TabRename, null);
        prompt.HandleTextKey(new KeyEvent("u", KeyModifiers.Ctrl));
        prompt.TryExecute(ActionIds.InputAccept, null);

        Assert.Null(setup.Host.Workspace.ActiveTab!.CustomName);
        Assert.Equal("repo0", setup.Host.Workspace.ActiveTab.Title);
    }

    [Fact]
    public void GivenTheRenamePrompt_WhenCancelling_ThenTheNameIsUnchanged()
    {
        var setup = Create(1);
        var prompt = new PromptInputHandler(setup.Host);
        setup.Actions.TryExecute(ActionIds.TabRename, null);
        Type(prompt, "zzz");

        prompt.TryExecute(ActionIds.Cancel, null);

        Assert.Null(setup.Host.Workspace.ActiveTab!.CustomName);
        Assert.Equal(KeyMode.Normal, setup.Host.State.Shell.Mode);
    }

    [Fact]
    public void GivenNoTab_WhenRenaming_ThenNoPromptOpens()
    {
        var setup = Create(0);

        Assert.False(setup.Actions.TryExecute(ActionIds.TabRename, null));
        Assert.Null(setup.Host.State.Shell.Prompt);
    }

    private static void OpenPicker(Dispatcher host) =>
        host.Dispatch(new PickerOpenRequested(null, string.Empty, host.Clock.GetUtcNow()));

    private static void Type(PromptInputHandler prompt, string text)
    {
        foreach (var character in text)
        {
            prompt.HandleTextKey(new KeyEvent(character.ToString()));
        }
    }

    private static Setup Create(int tabCount, bool withUnsentMessage = false)
    {
        var shell = new TestShell();
        var host = shell.Host;
        for (var index = 0; index < tabCount; index++)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-workspace-actions", $"repo{index}");
            var gitDirectory = Path.Combine(root, ".git");
            host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, gitDirectory, gitDirectory, false, false)));
        }

        if (withUnsentMessage)
        {
            foreach (var tab in host.Workspace.Tabs)
            {
                host.Dispatch(new CommitDraftRestored(tab.Id, new SessionDraft("unsent", false, false)));
            }
        }

        return new Setup(new WorkspaceActions(host), host);
    }

    private sealed record Setup(WorkspaceActions Actions, Dispatcher Host);
}
