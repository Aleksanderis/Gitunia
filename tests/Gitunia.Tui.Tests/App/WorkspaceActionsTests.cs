using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class WorkspaceActionsTests
{
    [Fact]
    public void GivenThreeTabs_WhenATabClickRunsTabNextWithItsNumber_ThenThatTabIsActive()
    {
        var (actions, host, _) = Create(3);

        var handled = actions.TryExecute(ActionIds.TabNext, 3);

        Assert.True(handled);
        Assert.Equal(host.Workspace.Tabs[2].Id, host.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenATabIsClicked_ThenThePickerClosesAndTheTabIsActive()
    {
        var (actions, host, picker) = Create(2);
        picker.Open();

        actions.TryExecute(ActionIds.TabNext, 1);

        Assert.False(picker.IsOpen);
        Assert.Equal(host.Workspace.Tabs[0].Id, host.Workspace.ActiveTabId);
    }

    [Fact]
    public void GivenNoTabs_WhenATabClickArrives_ThenNothingHappens()
    {
        var (actions, host, _) = Create(0);

        var handled = actions.TryExecute(ActionIds.TabNext, 1);

        Assert.True(handled);
        Assert.Empty(host.Workspace.Tabs);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenTheMouseDoubleClickActionRuns_ThenThePickerHandlesIt()
    {
        var (actions, _, picker) = Create(0);
        picker.Open();

        var handled = actions.TryExecute(ActionIds.PickerAccept, null);

        Assert.True(handled);
    }

    [Fact]
    public void GivenAnOpenPickerWithAQuery_WhenPickerOpenRunsAgain_ThenTheStateIsKept()
    {
        var (actions, _, picker) = Create(1);
        picker.Open();
        picker.Apply(new TypeTextCommand("abc"));

        actions.TryExecute(ActionIds.PickerOpen, null);

        Assert.Equal("abc", picker.State!.Query);
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenClosingTheTab_ThenTheFirstCloseOnlyWarnsAndTheSecondCloses()
    {
        var (actions, host, _) = Create(2, hasUnsentMessage: _ => true);

        actions.TryExecute(ActionIds.TabClose, null);

        Assert.Equal(2, host.Workspace.Tabs.Length);
        Assert.Contains("Unsent commit message", actions.Message, StringComparison.Ordinal);

        actions.TryExecute(ActionIds.TabClose, null);

        Assert.Single(host.Workspace.Tabs);
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenAnotherActionComesBetweenTheCloses_ThenTheGuardWarnsAgain()
    {
        var (actions, host, _) = Create(2, hasUnsentMessage: _ => true);
        actions.TryExecute(ActionIds.TabClose, null);

        actions.TryExecute(ActionIds.TabLastActive, null);
        actions.TryExecute(ActionIds.TabClose, null);

        Assert.Equal(2, host.Workspace.Tabs.Length);
    }

    [Fact]
    public void GivenNoUnsentMessage_WhenClosingTheTab_ThenItClosesAtOnce()
    {
        var (actions, host, _) = Create(2, hasUnsentMessage: _ => false);

        actions.TryExecute(ActionIds.TabClose, null);

        Assert.Single(host.Workspace.Tabs);
    }

    [Fact]
    public void GivenATab_WhenRenamingAndAccepting_ThenTheTypedNameIsTheTabTitle()
    {
        var (actions, host, _) = Create(1);
        var prompt = new PromptInputHandler(host);
        var tab = host.Workspace.ActiveTab!;

        Assert.True(actions.TryExecute(ActionIds.TabRename, null));
        Assert.Equal(KeyMode.Insert, host.State.Shell.Mode);
        Assert.Contains("repo0_", PopupText.Of(prompt), StringComparison.Ordinal);
        Type(prompt, "-2");
        prompt.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal("repo0-2", host.Workspace.FindTab(tab.Id)!.Title);
        Assert.Equal("repo0", host.Workspace.FindTab(tab.Id)!.DisplayName);
        Assert.Equal(KeyMode.Normal, host.State.Shell.Mode);
        Assert.Equal(string.Empty, PopupText.Of(prompt));
    }

    [Fact]
    public void GivenARenamedTab_WhenAcceptingAnEmptyName_ThenTheRepositoryNameIsBack()
    {
        var (actions, host, _) = Create(1);
        var prompt = new PromptInputHandler(host);
        actions.TryExecute(ActionIds.TabRename, null);
        Type(prompt, "x");
        prompt.TryExecute(ActionIds.InputAccept, null);

        actions.TryExecute(ActionIds.TabRename, null);
        prompt.HandleTextKey(new KeyEvent("u", KeyModifiers.Ctrl));
        prompt.TryExecute(ActionIds.InputAccept, null);

        Assert.Null(host.Workspace.ActiveTab!.CustomName);
        Assert.Equal("repo0", host.Workspace.ActiveTab.Title);
    }

    [Fact]
    public void GivenTheRenamePrompt_WhenCancelling_ThenTheNameIsUnchanged()
    {
        var (actions, host, _) = Create(1);
        var prompt = new PromptInputHandler(host);
        actions.TryExecute(ActionIds.TabRename, null);
        Type(prompt, "zzz");

        prompt.TryExecute(ActionIds.Cancel, null);

        Assert.Null(host.Workspace.ActiveTab!.CustomName);
        Assert.Equal(KeyMode.Normal, host.State.Shell.Mode);
    }

    [Fact]
    public void GivenNoTab_WhenRenaming_ThenNoPromptOpens()
    {
        var (actions, host, _) = Create(0);

        Assert.False(actions.TryExecute(ActionIds.TabRename, null));
        Assert.Null(host.State.Shell.Prompt);
    }

    private static void Type(PromptInputHandler prompt, string text)
    {
        foreach (var character in text)
        {
            prompt.HandleTextKey(new KeyEvent(character.ToString()));
        }
    }

    private static WorkspaceActionsHarness Create(int tabCount, Func<TabId, bool>? hasUnsentMessage = null)
    {
        var host = new TestShell().Host;
        for (var index = 0; index < tabCount; index++)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-workspace-actions", $"repo{index}");
            host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false)));
        }

        // Background results are queued and never run: these tests are synchronous and must not race a scan on another thread.
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var picker = new PickerController(TestGit.Services.Discovery, host, Path.GetTempPath(), pump.Post);
        return new WorkspaceActionsHarness(new WorkspaceActions(host, picker, () => { }, hasUnsentMessage), host, picker);
    }
}
