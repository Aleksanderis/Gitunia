using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;

namespace Gitunia.Core.Input;

/// <summary>
/// The one place that says who takes a key, a typed character or a click first (docs/internal/ux.md, "Focus model
/// and key style"): an open screen or editor, then the overlay layer, then the workspace actions, then the prompts,
/// then the focused pane. Pure; the handlers themselves still check that their own overlay or prompt is open, so an
/// entry here only says who is asked, and in which order.
/// </summary>
public static class InputRouting
{
    private static readonly ImmutableArray<InputHandler> ScreenLayer =
    [
        InputHandler.Settings,
        InputHandler.ConflictEditor,
        InputHandler.RebaseEditor,
        InputHandler.Reflog,
        InputHandler.Blame,
        InputHandler.Overlays,
        InputHandler.Creation,
        InputHandler.WorkspaceActions,
    ];

    // Prompts that live in state or own Esc and Enter come first, so no pane sees those keys while one is open.
    private static readonly ImmutableArray<InputHandler> PromptsAndGlobalActions =
    [
        InputHandler.OpenPrompt,
        InputHandler.QuickJump,
        InputHandler.StatusActions,
        InputHandler.Refresh,
        InputHandler.Branches,
        InputHandler.Stash,
        InputHandler.Tags,
    ];

    // Sync follows the tags (except Esc, which only cancels a running transfer once everything else passed).
    private static readonly ImmutableArray<InputHandler> RepositoryActions =
    [
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
    ];

    // The find-as-you-type prompts work on the graph; in other panes only their accept and cancel actions apply.
    private static readonly ImmutableArray<InputHandler> GraphPrompts =
    [
        InputHandler.WorkingDiff,
        InputHandler.RefJump,
        InputHandler.Search,
    ];

    private static readonly ImmutableArray<InputHandler> TextKeys =
    [
        InputHandler.Settings,
        InputHandler.RebaseEditor,
        InputHandler.ConflictEditor,
        InputHandler.Reflog,
        InputHandler.Blame,
        InputHandler.Diff,
        InputHandler.WorkingDiff,
        InputHandler.QuickJump,
        InputHandler.FileTree,
        InputHandler.OpenPrompt,
        InputHandler.Pick,
        InputHandler.RefJump,
        InputHandler.Search,
        InputHandler.CommitMessage,
    ];

    private static readonly ImmutableArray<OverlayClickTarget> Clicks =
    [
        OverlayClickTarget.Settings,
        OverlayClickTarget.ConflictEditor,
        OverlayClickTarget.RebaseEditor,
        OverlayClickTarget.Reflog,
        OverlayClickTarget.Blame,
        OverlayClickTarget.HelpTab,
        OverlayClickTarget.Palette,
        OverlayClickTarget.ContextMenu,
        OverlayClickTarget.Mainline,
        OverlayClickTarget.Confirm,
    ];

    /// <summary>The handlers to offer an action to, in order; empty when it must not run at all right now.</summary>
    /// <param name="action">The action.</param>
    /// <param name="shell">The shell state: which screens and editors are open.</param>
    /// <param name="focus">The overlay of the overlay layer and the focused list.</param>
    public static ImmutableArray<InputHandler> Handlers(ActionId action, ShellState shell, InputFocus focus)
    {
        if (IsBlocked(action, shell, focus))
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<InputHandler>();
        builder.AddRange(ScreenLayer);
        builder.AddRange(PromptsAndGlobalActions);
        if (action != ActionIds.Cancel)
        {
            builder.Add(InputHandler.Sync);
        }

        builder.AddRange(RepositoryActions);
        if (focus.IsGraphFocused || action == ActionIds.InputAccept || action == ActionIds.Cancel)
        {
            builder.AddRange(GraphPrompts);
        }

        if (action == ActionIds.Cancel)
        {
            builder.Add(InputHandler.Sync);
        }

        builder.Add(InputHandler.Compare);
        builder.Add(InputHandler.CreationEscape);
        builder.Add(InputHandler.Navigation);

        return builder.ToImmutable();
    }

    /// <summary>Who gets a typed key in Insert or Command mode, in order.</summary>
    public static ImmutableArray<InputHandler> TextKeyHandlers() => TextKeys;

    /// <summary>What a click on an overlay line may hit, in order.</summary>
    public static ImmutableArray<OverlayClickTarget> ClickTargets() => Clicks;

    /// <summary>True for the handlers of the prompts and the panes, asked only when no screen took the action.</summary>
    public static bool IsPaneLayer(InputHandler handler) => !ScreenLayer.Contains(handler);

    // The settings do not open over another overlay, and <space>em (global, #742) must not start git mergetool under
    // an open overlay or editor.
    private static bool IsBlocked(ActionId action, ShellState shell, InputFocus focus)
    {
        var isSettingsOpener = action == ActionIds.SettingsOpen || action == ActionIds.SettingsOpenTheme;
        if (!isSettingsOpener && action != ActionIds.ToolMergetool)
        {
            return false;
        }

        var isCovered = focus.IsOverlayOpen || shell.HasEditor;

        return isCovered || (action == ActionIds.ToolMergetool && shell.Settings.IsOpen);
    }
}
