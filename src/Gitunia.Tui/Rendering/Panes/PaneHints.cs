using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>The key hints the status bar shows for the focused pane and for the repository picker. Help, commands and back come first so a narrow terminal cuts the pane's own hints, not those.</summary>
public static class PaneHints
{
    private static readonly ImmutableArray<KeyHint> Pinned =
    [
        new("?", "help", [ActionIds.HelpShow]),
        new(":", "commands", [ActionIds.PaletteOpen]),
        new("q", "quit", [ActionIds.AppQuit]),
    ];

    private static readonly ImmutableArray<KeyHint> Graph =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>s", "working tree", [ActionIds.WorkingTreeToggle]),
        new("gg/G", "top/end", [ActionIds.ListTop, ActionIds.ListBottom]),
        new("Ctrl-d/u", "page", [ActionIds.ListHalfPageDown, ActionIds.ListHalfPageUp]),
        new("<space>h", "HEAD", [ActionIds.GraphJumpToHead]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
    ];

    private static readonly ImmutableArray<KeyHint> Sidebar =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>s", "working tree", [ActionIds.WorkingTreeToggle]),
        new("l/Enter", "expand", [ActionIds.SidebarExpand, ActionIds.SidebarActivate]),
        new("h", "collapse", [ActionIds.SidebarCollapse]),
        new("/", "filter", [ActionIds.SidebarFilter]),
    ];

    private static readonly ImmutableArray<KeyHint> Detail =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>s", "working tree", [ActionIds.WorkingTreeToggle]),
        new("]/[", "tabs", [ActionIds.DetailNextTab, ActionIds.DetailPreviousTab]),
        new("l", "open", [ActionIds.DetailOpenDiff]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("h", "back", [ActionIds.DetailCollapse]),
        new("<space>r", "retry", [ActionIds.DetailRetry]),
    ];

    private static readonly ImmutableArray<KeyHint> Diff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("]c/[c", "hunk", [ActionIds.DiffNextHunk, ActionIds.DiffPreviousHunk]),
        new("]f/[f", "file", [ActionIds.DiffNextFile, ActionIds.DiffPreviousFile]),
        new("/", "search", [ActionIds.DiffSearchOpen]),
        new("n/N", "next/prev match", [ActionIds.DiffSearchNext, ActionIds.DiffSearchPrevious]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>vw", "wrap", [ActionIds.DiffToggleWrap]),
        new("+/-", "context", [ActionIds.DiffMoreContext, ActionIds.DiffLessContext]),
        new("<space>vf", "whole file", [ActionIds.DiffToggleWholeFile]),
        new("<space>vi", "ignore space", [ActionIds.DiffToggleIgnoreWhitespace]),
        new("<space>vs", "side by side", [ActionIds.DiffToggleSideBySide]),
        new("<space>vl", "load all", [ActionIds.DiffLoadAnyway]),
        new("h", "back", [ActionIds.NavigateBack]),
    ];

    private static readonly KeyHint PaneJumps = new(
        "<space>1/2/3/4",
        "panes",
        [
            ActionIds.WorkingTreeFocusUnstaged,
            ActionIds.WorkingTreeFocusStaged,
            ActionIds.WorkingTreeFocusDiff,
            ActionIds.WorkingTreeFocusMessage,
        ]);

    private static readonly ImmutableArray<KeyHint> WorkingDiffCommon =
    [
        new("]c/[c", "change", [ActionIds.DiffNextHunk, ActionIds.DiffPreviousHunk]),
        new("/", "search", [ActionIds.DiffSearchOpen]),
        new("n/N", "next/prev match", [ActionIds.DiffSearchNext, ActionIds.DiffSearchPrevious]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>vw", "wrap", [ActionIds.DiffToggleWrap]),
        new("+/-", "context", [ActionIds.DiffMoreContext, ActionIds.DiffLessContext]),
        new("<space>vf", "whole file", [ActionIds.DiffToggleWholeFile]),
        new("<space>vi", "ignore space", [ActionIds.DiffToggleIgnoreWhitespace]),
        new("<space>vs", "side by side", [ActionIds.DiffToggleSideBySide]),
        new("<space>vl", "load all", [ActionIds.DiffLoadAnyway]),
        new("y", "copy line", [ActionIds.DiffCopyLine]),
        PaneJumps,
        new("h", "back", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstagedDiff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>a", "stage change", [ActionIds.WorkingTreeStage]),
        new("<space>d", "discard", [ActionIds.WorkingTreeDiscard]),
        new("v", "select lines", [ActionIds.VisualToggle]),
        .. WorkingDiffCommon,
    ];

    private static readonly ImmutableArray<KeyHint> WorkingStagedDiff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>u", "unstage change", [ActionIds.WorkingTreeUnstage]),
        new("v", "select lines", [ActionIds.VisualToggle]),
        .. WorkingDiffCommon,
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstaged =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>a", "stage", [ActionIds.WorkingTreeStage]),
        new("<space>A", "stage all", [ActionIds.WorkingTreeStageAll]),
        new("l/Enter", "diff", [ActionIds.WorkingTreeOpenDiff, ActionIds.WorkingTreeOpenDiff]),
        new("<space>d", "discard", [ActionIds.WorkingTreeDiscard]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>c", "message", [ActionIds.WorkingTreeWriteMessage]),
        PaneJumps,
        new("y", "copy path", [ActionIds.WorkingTreeCopyPath]),
        new("<space>r", "refresh", [ActionIds.RepositoryRefresh]),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingStaged =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>u", "unstage", [ActionIds.WorkingTreeUnstage]),
        new("<space>U", "unstage all", [ActionIds.WorkingTreeUnstageAll]),
        new("l/Enter", "diff", [ActionIds.WorkingTreeOpenDiff, ActionIds.WorkingTreeOpenDiff]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>c", "message", [ActionIds.WorkingTreeWriteMessage]),
        PaneJumps,
        new("y", "copy path", [ActionIds.WorkingTreeCopyPath]),
        new("<space>r", "refresh", [ActionIds.RepositoryRefresh]),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingMessage =
    [
        .. Pinned,
        new("i", "write", [ActionIds.WorkingTreeEditMessage]),
        PaneJumps,
        new("<space>c", "commit", [ActionIds.WorkingTreeCommit]),
        new("Alt-s/Alt-n/Alt-a", "options", [ActionIds.WorkingTreeToggleSignOff, ActionIds.WorkingTreeToggleNoVerify, ActionIds.WorkingTreeToggleAmend]),
        new("<space>te", "$EDITOR", [ActionIds.ToolCommitEditor]),
    ];

    private static readonly ImmutableArray<KeyHint> MessageEditing =
    [
        new("Esc", "stop writing", [ActionIds.Cancel]),
        new("Alt-Enter", "commit", [ActionIds.WorkingTreeCommit]),
        new("Ctrl-g", "commit", [ActionIds.WorkingTreeCommit]),
        new("Alt-s/Alt-n/Alt-a", "options", [ActionIds.WorkingTreeToggleSignOff, ActionIds.WorkingTreeToggleNoVerify, ActionIds.WorkingTreeToggleAmend]),
    ];

    private static readonly ImmutableArray<KeyHint> Picker =
    [
        new("Enter", "open", [ActionIds.PickerAccept]),
        new("Tab", "accept", [ActionIds.PickerCompletePath]),
        new("Alt-p", "pin to top", [ActionIds.PickerPin]),
        new("Alt-d", "forget", [ActionIds.PickerRemoveRecent]),
        new("Alt-c", "clone", [ActionIds.PickerClone]),
        new("Alt-i", "init", [ActionIds.PickerInit]),
        new("Esc", "close", [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> Palette =
    [
        new("Ctrl-j/k", "move", [ActionIds.PaletteNext, ActionIds.PalettePrevious]),
        new("Enter", "run", [ActionIds.CommandExecute]),
        new("Esc", "close", [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> Confirm =
    [
        new("y", "yes, do it", [ActionIds.ConfirmYes]),
        new("n/Esc", "cancel", [ActionIds.ConfirmNo, ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> Reflog =
    [
        new("Enter", "select", [ActionIds.ReflogSelectCommit]),
        new("/", "search", [ActionIds.SearchOpen]),
        new("<space>l", "ref", [ActionIds.ReflogOpen]),
        new("<space>gx", "reset", [ActionIds.ResetBranch]),
        new("<space>bc", "branch", [ActionIds.BranchCreate]),
        new("<space>bs", "checkout", [ActionIds.BranchSwitch]),
        new("Esc", "close", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> RebaseEditor =
    [
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("p/r/s/f/d", "action", [
            ActionIds.RebaseEditorPick,
            ActionIds.RebaseEditorReword,
            ActionIds.RebaseEditorSquash,
            ActionIds.RebaseEditorFixup,
            ActionIds.RebaseEditorDrop,
        ]),
        new("J/K", "reorder", [ActionIds.RebaseEditorMoveDown, ActionIds.RebaseEditorMoveUp]),
        new("Enter", "start", [ActionIds.RebaseEditorStart]),
        new("Esc", "cancel", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> RebaseMessage =
    [
        new("Alt-Enter", "keep", [ActionIds.RebaseEditorMessageSave]),
        new("Enter", "new line", [ActionIds.RebaseEditorMessageNewLine]),
        new("Esc", "cancel", [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> Settings =
    [
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("l/Enter", "edit", [ActionIds.SettingsActivate, ActionIds.SettingsActivate]),
        new("h", "back", [ActionIds.NavigateBack]),
        new("/", "filter", [ActionIds.SettingsFilter]),
        new("Esc", "close", [ActionIds.NavigateBack]),
    ];

    public static IReadOnlyList<KeyHint> For(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => Sidebar,
        PaneKind.Detail => Detail,
        PaneKind.Diff => Diff,
        PaneKind.WorkingUnstaged => WorkingUnstaged,
        PaneKind.WorkingStaged => WorkingStaged,
        PaneKind.WorkingUnstagedDiff => WorkingUnstagedDiff,
        PaneKind.WorkingStagedDiff => WorkingStagedDiff,
        PaneKind.WorkingMessage => WorkingMessage,
        _ => Graph,
    };

    /// <summary>The hints while the commit message box is in Insert mode (typed keys are text, so only these act).</summary>
    public static IReadOnlyList<KeyHint> ForMessageEditing() => MessageEditing;

    /// <summary>The hints while a confirm dialog is open (only its answers act).</summary>
    public static IReadOnlyList<KeyHint> ForConfirm() => Confirm;

    /// <summary>The hints while the reflog list is open.</summary>
    public static IReadOnlyList<KeyHint> ForReflog() => Reflog;

    /// <summary>The hints while the rebase todo editor is open.</summary>
    public static IReadOnlyList<KeyHint> ForRebaseEditor() => RebaseEditor;

    /// <summary>The hints while a reword's message is edited in the rebase todo editor.</summary>
    public static IReadOnlyList<KeyHint> ForRebaseMessage() => RebaseMessage;

    /// <summary>The hints while the settings screen is open.</summary>
    public static IReadOnlyList<KeyHint> ForSettings() => Settings;

    /// <summary>The hints while the repository picker is open.</summary>
    public static IReadOnlyList<KeyHint> ForPicker() => Picker;

    /// <summary>The hints in the footer of the commands popup.</summary>
    public static IReadOnlyList<KeyHint> ForPalette() => Palette;
}
