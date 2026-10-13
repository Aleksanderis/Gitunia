using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class TabRenameControllerTests
{
    private readonly WorkspaceHost host = new(Workspace.Empty);
    private KeyMode mode = KeyMode.Normal;

    [Fact]
    public void GivenATab_WhenRenamingAndAccepting_ThenTheTypedNameIsTheTabTitle()
    {
        var controller = Create("alpha");

        Assert.True(controller.TryExecute(ActionIds.TabRename, null));
        Assert.Equal(KeyMode.Insert, mode);
        Assert.StartsWith("rename tab: alpha_", controller.StatusLine, StringComparison.Ordinal);
        Type(controller, "-2");
        controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal("alpha-2", host.Workspace.ActiveTab!.Title);
        Assert.Equal("alpha", host.Workspace.ActiveTab.DisplayName);
        Assert.Equal(KeyMode.Normal, mode);
        Assert.Equal(string.Empty, controller.StatusLine);
        Assert.Contains("alpha-2", controller.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARenamedTab_WhenAcceptingAnEmptyName_ThenTheRepositoryNameIsBack()
    {
        var controller = Create("alpha");
        controller.TryExecute(ActionIds.TabRename, null);
        Type(controller, "x");
        controller.TryExecute(ActionIds.InputAccept, null);

        controller.TryExecute(ActionIds.TabRename, null);
        controller.HandleTextKey(new KeyEvent("u", KeyModifiers.Ctrl));
        controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Null(host.Workspace.ActiveTab!.CustomName);
        Assert.Equal("alpha", host.Workspace.ActiveTab.Title);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenCancelling_ThenTheNameIsUnchanged()
    {
        var controller = Create("alpha");
        controller.TryExecute(ActionIds.TabRename, null);
        Type(controller, "zzz");

        controller.TryExecute(ActionIds.Cancel, null);

        Assert.Null(host.Workspace.ActiveTab!.CustomName);
        Assert.Equal(KeyMode.Normal, mode);
    }

    [Fact]
    public void GivenNoPrompt_WhenAcceptingOrTyping_ThenTheControllerLeavesTheKeysAlone()
    {
        var controller = Create("alpha");

        Assert.False(controller.TryExecute(ActionIds.InputAccept, null));
        Assert.False(controller.HandleTextKey(new KeyEvent("a")));
    }

    private static void Type(TabRenameController controller, string text)
    {
        foreach (var character in text)
        {
            controller.HandleTextKey(new KeyEvent(character.ToString()));
        }
    }

    private TabRenameController Create(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-rename", name);
        host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false)));
        return new TabRenameController(host, newMode => mode = newMode, () => { });
    }
}
