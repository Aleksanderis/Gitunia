using Gitunia.Core.Input;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.Input;
using Gitunia.Tui.Search;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The fourth stage: the prompts, the editors and the screens that open over the panes, which of them covers which, and
/// the typed-key handlers (registered here because the key controller needs them before the action handlers exist).
/// </summary>
public sealed class OverlayParts
{
    /// <param name="foundation">The configuration, the state and the widgets.</param>
    /// <param name="route">Turns a key into an action.</param>
    /// <param name="panes">The controllers of the panes.</param>
    public OverlayParts(ShellFoundation foundation, RouteParts route, PaneParts panes)
    {
        Store = route.Overlays;
        var host = foundation.Host;
        var glyphs = foundation.Config.Glyphs;
        Pick = new PickActions(host, panes.Picks);
        OpenPrompt = new PromptInputHandler(host);
        var graphView = new TerminalGraphView(foundation);
        RefJump = new RefJumpController(graphView);
        Search = new SearchController(
            graphView,
            foundation.Terminal,
            foundation.Terminal.Invoker,
            foundation.Services.CommitMessages);
        Prompts = new PromptDisplays(
            [panes.DiffPane, panes.WorkingDiff, panes.QuickJump, panes.FileTree, OpenPrompt, Pick, RefJump, Search]);
        Reflog = new ReflogInput(host, panes.Rows, glyphs);
        Blame = new BlameInput(
            host,
            foundation.Services.BlameStore,
            new BlameTargets(foundation.Panes, foundation.Sessions, panes.Graph),
            glyphs);
        Settings = new SettingsScreenInput(host, glyphs);
        Others = new OtherOverlays(route.Overlays, Reflog, Blame, Settings, Pick, Prompts);
        RebaseEditor = new RebaseEditorInput(host, panes.Rows, Others, glyphs, foundation.Keys);
        ConflictEditor = new ConflictEditorInput(host, glyphs, foundation.Keys);
        Editors = new EditorOverlays(RebaseEditor, ConflictEditor, Settings);
        Guard = new OverlayClickGuard();
        Cover = new OverlayCover(route.Overlays, Others, Editors, host, foundation.Gate);
        KeyFocus = new KeyFocus(panes.Picker, Cover);
        Handlers = RegisterTextKeyHandlers(panes);
    }

    /// <summary>The help, palette, menu, confirm dialog and output box.</summary>
    public OverlayStore Store { get; }

    /// <summary>The mainline question of a cherry-pick or revert.</summary>
    public PickActions Pick { get; }

    /// <summary>The open prompt (tab rename, tag, branch and so on).</summary>
    public PromptInputHandler OpenPrompt { get; }

    /// <summary>The ref jump prompt.</summary>
    public RefJumpController RefJump { get; }

    /// <summary>The search prompt.</summary>
    public SearchController Search { get; }

    /// <summary>The prompts that can be open.</summary>
    public PromptDisplays Prompts { get; }

    /// <summary>The reflog list.</summary>
    public ReflogInput Reflog { get; }

    /// <summary>The blame view.</summary>
    public BlameInput Blame { get; }

    /// <summary>The settings screen.</summary>
    public SettingsScreenInput Settings { get; }

    /// <summary>The overlays that cover the editors.</summary>
    public OtherOverlays Others { get; }

    /// <summary>The interactive rebase editor.</summary>
    public RebaseEditorInput RebaseEditor { get; }

    /// <summary>The conflict editor.</summary>
    public ConflictEditorInput ConflictEditor { get; }

    /// <summary>The editors and the settings screen.</summary>
    public EditorOverlays Editors { get; }

    /// <summary>Ignores a double-click right after a click on an overlay.</summary>
    public OverlayClickGuard Guard { get; }

    /// <summary>Which overlays are open.</summary>
    public OverlayCover Cover { get; }

    /// <summary>Which view has the keys.</summary>
    public KeyFocus KeyFocus { get; }

    /// <summary>The handlers registered so far; the action handlers join them in the next stage.</summary>
    public InputHandlersBuilder Handlers { get; }

    private InputHandlersBuilder RegisterTextKeyHandlers(PaneParts panes)
    {
        return new InputHandlersBuilder()
            .Add(InputHandler.Diff, panes.DiffPane)
            .Add(InputHandler.WorkingDiff, panes.WorkingDiff)
            .Add(InputHandler.OpenPrompt, OpenPrompt)
            .Add(InputHandler.Pick, Pick)
            .Add(InputHandler.Settings, Settings)
            .Add(InputHandler.ConflictEditor, ConflictEditor)
            .Add(InputHandler.RebaseEditor, RebaseEditor)
            .Add(InputHandler.Reflog, Reflog)
            .Add(InputHandler.Blame, Blame)
            .Add(InputHandler.FileTree, panes.FileTree)
            .Add(InputHandler.QuickJump, panes.QuickJump)
            .Add(InputHandler.RefJump, RefJump)
            .Add(InputHandler.Search, Search)
            .Add(InputHandler.CommitMessage, panes.Commits);
    }
}
