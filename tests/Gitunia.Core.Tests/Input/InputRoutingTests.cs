using System.Runtime.CompilerServices;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Input;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Input;

/// <summary>The precedence of docs/internal/ux.md, "Focus model and key style", as one tested order.</summary>
[Trait("Os", "Independent")]
public class InputRoutingTests
{
    private static readonly InputFocus GraphFocus = new(OverlayKind.None, true);

    private static readonly InputFocus DetailFocus = new(OverlayKind.None, false);

    [Fact]
    public void GivenAPlainActionInTheGraph_WhenRouting_ThenScreensComeBeforePromptsBeforePanes()
    {
        var handlers = InputRouting.Handlers(ActionIds.ListDown, ShellState.Empty, GraphFocus);

        Assert.Equal(
            [
                InputHandler.Settings,
                InputHandler.ConflictEditor,
                InputHandler.RebaseEditor,
                InputHandler.Reflog,
                InputHandler.Overlays,
                InputHandler.Creation,
                InputHandler.WorkspaceActions,
                InputHandler.OpenPrompt,
                InputHandler.QuickJump,
                InputHandler.StatusActions,
                InputHandler.Refresh,
                InputHandler.Branches,
                InputHandler.Stash,
                InputHandler.Tags,
                InputHandler.Sync,
                InputHandler.RemoteManagement,
                InputHandler.Merge,
                InputHandler.Pick,
                InputHandler.Reset,
                InputHandler.Rebase,
                InputHandler.WorkingTree,
                InputHandler.ExternalTools,
                InputHandler.Visual,
                InputHandler.PaneSizes,
                InputHandler.IssueLinks,
                InputHandler.FileTree,
                InputHandler.DetailTabs,
                InputHandler.Operations,
                InputHandler.Worktrees,
                InputHandler.Sidebar,
                InputHandler.Diff,
                InputHandler.WorkingDiff,
                InputHandler.RefJump,
                InputHandler.Search,
                InputHandler.Compare,
                InputHandler.CreationEscape,
                InputHandler.Navigation,
            ],
            handlers);
    }

    [Fact]
    public void GivenAnyAction_WhenRouting_ThenNobodyIsAskedTwiceAndTheListEndsWithTheNavigation()
    {
        foreach (var action in new[] { ActionIds.ListDown, ActionIds.Cancel, ActionIds.InputAccept })
        {
            foreach (var focus in new[] { GraphFocus, DetailFocus })
            {
                var handlers = InputRouting.Handlers(action, ShellState.Empty, focus);

                Assert.Equal(handlers.Distinct().Count(), handlers.Length);
                Assert.Equal(InputHandler.Navigation, handlers[^1]);
            }
        }
    }

    [Fact]
    public void GivenAnOpenPromptOrQuickJump_WhenRouting_ThenTheyAreAskedBeforeEveryPaneHandler()
    {
        var handlers = InputRouting.Handlers(ActionIds.InputAccept, ShellState.Empty, DetailFocus);

        var firstPane = handlers.ToList().FindIndex(InputRouting.IsPaneLayer);
        Assert.Equal(InputHandler.OpenPrompt, handlers[firstPane]);
        Assert.Equal(InputHandler.QuickJump, handlers[firstPane + 1]);
    }

    [Fact]
    public void GivenAnOverlayOfTheOverlayLayer_WhenRouting_ThenItIsAskedBeforeTheWorkspaceActionsAndThePanes()
    {
        var focus = new InputFocus(OverlayKind.Palette, true);

        var handlers = InputRouting.Handlers(ActionIds.Cancel, ShellState.Empty, focus);

        var overlays = handlers.ToList().IndexOf(InputHandler.Overlays);
        Assert.True(overlays < handlers.ToList().IndexOf(InputHandler.WorkspaceActions));
        Assert.True(overlays < handlers.ToList().FindIndex(InputRouting.IsPaneLayer));
    }

    [Fact]
    public void GivenTheDetailPane_WhenAPlainActionIsRouted_ThenTheFindAsYouTypePromptsAreNotAsked()
    {
        var handlers = InputRouting.Handlers(ActionIds.ListDown, ShellState.Empty, DetailFocus);

        Assert.DoesNotContain(InputHandler.Search, handlers);
        Assert.DoesNotContain(InputHandler.RefJump, handlers);
        Assert.DoesNotContain(InputHandler.WorkingDiff, handlers);
    }

    [Theory]
    [InlineData("input.accept")]
    [InlineData("cancel")]
    public void GivenTheDetailPane_WhenAcceptOrCancelIsRouted_ThenTheFindAsYouTypePromptsAreAsked(string action)
    {
        var handlers = InputRouting.Handlers(new ActionId(action), ShellState.Empty, DetailFocus);

        Assert.Contains(InputHandler.Search, handlers);
        Assert.Contains(InputHandler.RefJump, handlers);
        Assert.Contains(InputHandler.WorkingDiff, handlers);
    }

    [Fact]
    public void GivenEsc_WhenRouting_ThenTheSyncCancelComesAfterThePromptsAndBeforeTheCompareMark()
    {
        var handlers = InputRouting.Handlers(ActionIds.Cancel, ShellState.Empty, GraphFocus).ToList();

        Assert.Single(handlers, InputHandler.Sync);
        Assert.True(handlers.IndexOf(InputHandler.Sync) > handlers.IndexOf(InputHandler.Search));
        Assert.True(handlers.IndexOf(InputHandler.Sync) < handlers.IndexOf(InputHandler.Compare));
    }

    [Fact]
    public void GivenAnActionThatIsNotEsc_WhenRouting_ThenSyncIsAskedOnceBeforeTheOtherRepositoryActions()
    {
        var handlers = InputRouting.Handlers(ActionIds.RemoteFetch, ShellState.Empty, GraphFocus).ToList();

        Assert.Single(handlers, InputHandler.Sync);
        Assert.Equal(handlers.IndexOf(InputHandler.Tags) + 1, handlers.IndexOf(InputHandler.Sync));
    }

    [Fact]
    public void GivenNothingOpen_WhenSettingsOrMergetoolAreRouted_ThenTheyAreOffered()
    {
        Assert.NotEmpty(InputRouting.Handlers(ActionIds.SettingsOpen, ShellState.Empty, GraphFocus));
        Assert.NotEmpty(InputRouting.Handlers(ActionIds.SettingsOpenTheme, ShellState.Empty, GraphFocus));
        Assert.NotEmpty(InputRouting.Handlers(ActionIds.ToolMergetool, ShellState.Empty, GraphFocus));
    }

    [Theory]
    [InlineData("help")]
    [InlineData("palette")]
    [InlineData("menu")]
    [InlineData("confirm")]
    [InlineData("reflog")]
    [InlineData("rebase")]
    [InlineData("conflict")]
    public void GivenAnOverlayOrEditor_WhenSettingsOrMergetoolAreRouted_ThenNothingRuns(string open)
    {
        var shell = ShellWith(open);
        var focus = FocusWith(open);

        Assert.Empty(InputRouting.Handlers(ActionIds.SettingsOpen, shell, focus));
        Assert.Empty(InputRouting.Handlers(ActionIds.SettingsOpenTheme, shell, focus));
        Assert.Empty(InputRouting.Handlers(ActionIds.ToolMergetool, shell, focus));
    }

    [Fact]
    public void GivenTheSettingsOpen_WhenSettingsAreRouted_ThenTheScreenStillGetsThemButMergetoolIsBlocked()
    {
        var shell = ShellWith("settings");

        Assert.Equal(InputHandler.Settings, InputRouting.Handlers(ActionIds.SettingsOpen, shell, GraphFocus)[0]);
        Assert.Empty(InputRouting.Handlers(ActionIds.ToolMergetool, shell, GraphFocus));
    }

    [Fact]
    public void GivenAnOverlayOpen_WhenOtherActionsAreRouted_ThenNothingIsBlocked()
    {
        var shell = ShellWith("conflict");

        Assert.NotEmpty(InputRouting.Handlers(ActionIds.ListDown, shell, GraphFocus));
        Assert.NotEmpty(InputRouting.Handlers(ActionIds.ToolDifftool, shell, GraphFocus));
    }

    [Fact]
    public void GivenTheScreenLayer_WhenAskingForThePaneLayer_ThenOnlyTheLaterHandlersCount()
    {
        Assert.False(InputRouting.IsPaneLayer(InputHandler.Settings));
        Assert.False(InputRouting.IsPaneLayer(InputHandler.Overlays));
        Assert.False(InputRouting.IsPaneLayer(InputHandler.WorkspaceActions));
        Assert.True(InputRouting.IsPaneLayer(InputHandler.OpenPrompt));
        Assert.True(InputRouting.IsPaneLayer(InputHandler.Navigation));
    }

    [Fact]
    public void GivenTypedKeys_WhenRouting_ThenTheScreensAndEditorsComeBeforeThePrompts()
    {
        var handlers = InputRouting.TextKeyHandlers();

        Assert.Equal(
            [
                InputHandler.Settings,
                InputHandler.RebaseEditor,
                InputHandler.ConflictEditor,
                InputHandler.Reflog,
            ],
            handlers.Take(4));
        Assert.True(handlers.IndexOf(InputHandler.OpenPrompt) > handlers.IndexOf(InputHandler.Reflog));
        Assert.Equal(InputHandler.CommitMessage, handlers[^1]);
        Assert.Equal(handlers.Distinct().Count(), handlers.Length);
    }

    [Fact]
    public void GivenAClickOnAnOverlayLine_WhenRouting_ThenTheScreensComeBeforeTheOverlayLayerAndTheConfirmIsLast()
    {
        var targets = InputRouting.ClickTargets();

        Assert.Equal(OverlayClickTarget.Settings, targets[0]);
        Assert.True(targets.IndexOf(OverlayClickTarget.Reflog) < targets.IndexOf(OverlayClickTarget.HelpTab));
        Assert.True(targets.IndexOf(OverlayClickTarget.Palette) < targets.IndexOf(OverlayClickTarget.ContextMenu));
        Assert.Equal(OverlayClickTarget.Confirm, targets[^1]);
        Assert.Equal(Enum.GetValues<OverlayClickTarget>().Length, targets.Length);
    }

    [Fact]
    public void GivenEveryHandler_WhenCollectingTheRoutes_ThenEachIsReachableByAnActionOrATypedKey()
    {
        var reachable = new HashSet<InputHandler>(InputRouting.TextKeyHandlers());
        foreach (var focus in new[] { GraphFocus, DetailFocus })
        {
            foreach (var action in new[] { ActionIds.ListDown, ActionIds.Cancel })
            {
                reachable.UnionWith(InputRouting.Handlers(action, ShellState.Empty, focus));
            }
        }

        Assert.Equal(Enum.GetValues<InputHandler>().Order(), reachable.Order());
    }

    private static InputFocus FocusWith(string open) => open switch
    {
        "help" => new InputFocus(OverlayKind.Help, true),
        "palette" => new InputFocus(OverlayKind.Palette, true),
        "menu" => new InputFocus(OverlayKind.ContextMenu, true),
        "confirm" => new InputFocus(OverlayKind.Confirm, true),
        _ => GraphFocus,
    };

    private static ShellState ShellWith(string open) => open switch
    {
        "reflog" => ShellState.Empty with
        {
            Reflog = ReflogSession.Empty with { List = new ReflogState(new TabId(1), "HEAD", "HEAD", []) },
        },
        "rebase" => ShellState.Empty with
        {
            RebaseEditor = RebaseEditorSession.Empty with { Editor = Uninitialised<RebaseEditorState>() },
        },
        "conflict" => ShellState.Empty with
        {
            Conflict = ConflictSession.Empty with { Editor = Uninitialised<ConflictEditorState>() },
        },
        "settings" => ShellState.Empty with
        {
            Settings = SettingsSession.Empty with { Screen = Uninitialised<SettingsState>() },
        },
        _ => ShellState.Empty,
    };

    // Only whether the state is open is read, so the contents do not matter.
    private static T Uninitialised<T>()
        where T : class
    {
#pragma warning disable IL2087 // Reflection is fine in tests; they are never trimmed.
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
#pragma warning restore IL2087
    }
}
