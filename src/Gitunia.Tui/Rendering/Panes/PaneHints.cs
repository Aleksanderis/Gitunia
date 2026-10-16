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
        new("l", "open diff", [ActionIds.DetailOpenDiff]),
        new("h", "back", [ActionIds.NavigateBack]),
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
        new("Tab", "next pane", [ActionIds.PaneNext]),
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
        new("Tab", "next pane", [ActionIds.PaneNext]),
        new("y", "copy path", [ActionIds.WorkingTreeCopyPath]),
        new("<space>r", "refresh", [ActionIds.RepositoryRefresh]),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingMessage =
    [
        .. Pinned,
        new("i", "write", [ActionIds.WorkingTreeEditMessage]),
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
        new("Esc", "close", [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> Confirm =
    [
        new("y", "yes, do it", [ActionIds.ConfirmYes]),
        new("n/Esc", "cancel", [ActionIds.ConfirmNo, ActionIds.Cancel]),
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

    /// <summary>The hints while the repository picker is open.</summary>
    public static IReadOnlyList<KeyHint> ForPicker() => Picker;
}
