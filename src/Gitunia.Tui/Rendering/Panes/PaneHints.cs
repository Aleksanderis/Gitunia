using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Repeat;
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

    private const string ConflictEditorLabel = "conflict editor";

    private const string MergetoolLabel = "mergetool";

    private static readonly KeyHint ConflictEditorHint = new(
        "<space>to",
        ConflictEditorLabel,
        [ActionIds.ConflictOpen]);

    // For a conflicted file the editor refuses (delete/modify and the like): the mergetool is the way (#742).
    private static readonly KeyHint ConflictMergetoolHint = new(
        "<space>tm",
        MergetoolLabel,
        [ActionIds.ToolMergetool]);

    // Ctrl-h/j/k/l move between panes in every view; bare h/j/k/l act inside the pane (ux.md, "Moving focus").
    private static readonly KeyHint PaneMove = new(
        "Ctrl-h/j/k/l",
        "pane",
        [ActionIds.PaneFocusLeft, ActionIds.PaneFocusDown, ActionIds.PaneFocusUp, ActionIds.PaneFocusRight]);

    // A hint without actions names a chord group (the which-key popup lists its continuations): the test checks the
    // keymap binds something below the prefix instead of one action per key.
    private static readonly KeyHint ViewOptions = new("<space>v", "view options", []);

    private static readonly ImmutableArray<KeyHint> Graph =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>s", "working tree", [ActionIds.WorkingTreeToggle]),
        new("gg/G", "top/end", [ActionIds.ListTop, ActionIds.ListBottom]),
        new("Ctrl-d/u", "page", [ActionIds.ListHalfPageDown, ActionIds.ListHalfPageUp]),
        new("<space>h", "HEAD", [ActionIds.GraphJumpToHead]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> GraphComparing =
    [
        .. Pinned,
        new("<space>=", "move mark", [ActionIds.GraphCompare]),
        new("Esc", "end compare", [ActionIds.Cancel]),
        .. Graph.Skip(Pinned.Length),
    ];

    private static readonly ImmutableArray<KeyHint> Sidebar =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>s", "working tree", [ActionIds.WorkingTreeToggle]),
        new("l/Enter", "expand", [ActionIds.SidebarExpand, ActionIds.SidebarActivate]),
        new("h", "collapse", [ActionIds.SidebarCollapse]),
        new("/", "filter", [ActionIds.SidebarFilter]),
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> Detail =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>s", "working tree", [ActionIds.WorkingTreeToggle]),
        new("]/[", "tabs", [ActionIds.DetailNextTab, ActionIds.DetailPreviousTab]),
        new("Enter", "open", [ActionIds.DetailOpenDiff]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("Esc", "back", [ActionIds.NavigateBack]),
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> DetailChanges =
    [
        .. Detail,
        new("za", "fold", [ActionIds.DetailToggleFolder]),
        ViewOptions,
    ];

    private static readonly ImmutableArray<KeyHint> Diff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("]c/[c", "hunk", [ActionIds.DiffNextHunk, ActionIds.DiffPreviousHunk]),
        new("]f/[f", "file", [ActionIds.DiffNextFile, ActionIds.DiffPreviousFile]),
        new("/", "search", [ActionIds.DiffSearchOpen]),
        ViewOptions,
        new("Esc", "back", [ActionIds.NavigateBack]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> FileContent =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("/", "search", [ActionIds.DiffSearchOpen]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>vw", "wrap", [ActionIds.DiffToggleWrap]),
        new("y", "copy line", [ActionIds.DiffCopyLine]),
        new("Y", "copy file", [ActionIds.DiffCopyFile]),
        new("Esc", "back", [ActionIds.NavigateBack]),
        PaneMove,
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
        ViewOptions,
        new("Esc", "back", [ActionIds.NavigateBack]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        PaneJumps,
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstagedDiff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>a", "stage change", [ActionIds.WorkingTreeStage]),
        new("<space>d", "discard", [ActionIds.WorkingTreeDiscard]),
        new("v", "select lines", [ActionIds.VisualToggle]),
        .. WorkingDiffCommon,
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> WorkingStagedDiff =
    [
        .. Pinned,
        new("j/k", "scroll", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>u", "unstage change", [ActionIds.WorkingTreeUnstage]),
        new("v", "select lines", [ActionIds.VisualToggle]),
        .. WorkingDiffCommon,
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstaged =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>a", "stage", [ActionIds.WorkingTreeStage]),
        new("<space>A", "stage all", [ActionIds.WorkingTreeStageAll]),
        new("Enter", "diff", [ActionIds.WorkingTreeOpenDiff]),
        new("<space>d", "discard", [ActionIds.WorkingTreeDiscard]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>c", "message", [ActionIds.WorkingTreeWriteMessage]),
        PaneJumps,
        PaneMove,
    ];

    // The cursor is on a conflicted file: the editor is the first pane hint, so a narrow bar cuts the others (#742).
    private static readonly ImmutableArray<KeyHint> WorkingUnstagedConflict =
    [
        .. Pinned,
        new("Enter", ConflictEditorLabel, [ActionIds.WorkingTreeOpenDiff]),
        .. WorkingUnstaged.Skip(Pinned.Length).Where(hint => hint.Description != "diff"),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstagedDiffConflict =
    [
        .. Pinned,
        ConflictEditorHint,
        .. WorkingUnstagedDiff.Skip(Pinned.Length),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstagedRefused =
    [
        .. Pinned,
        ConflictMergetoolHint,
        .. WorkingUnstaged.Skip(Pinned.Length).Where(hint => hint.Description != "diff"),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingUnstagedDiffRefused =
    [
        .. Pinned,
        ConflictMergetoolHint,
        .. WorkingUnstagedDiff.Skip(Pinned.Length),
    ];

    private static readonly ImmutableArray<KeyHint> WorkingStaged =
    [
        .. Pinned,
        new("j/k", "move", [ActionIds.ListDown, ActionIds.ListUp]),
        new("<space>u", "unstage", [ActionIds.WorkingTreeUnstage]),
        new("<space>U", "unstage all", [ActionIds.WorkingTreeUnstageAll]),
        new("Enter", "diff", [ActionIds.WorkingTreeOpenDiff]),
        new("<space>j", "quick jump", [ActionIds.QuickJump]),
        new("<space>c", "message", [ActionIds.WorkingTreeWriteMessage]),
        PaneJumps,
        PaneMove,
    ];

    private static readonly ImmutableArray<KeyHint> WorkingMessage =
    [
        .. Pinned,
        new("i", "write", [ActionIds.WorkingTreeEditMessage]),
        PaneMove,
        PaneJumps,
        new("<space>c", "commit", [ActionIds.WorkingTreeCommit]),
        new("Alt-s/Alt-n/Alt-a", "options", [ActionIds.WorkingTreeToggleSignOff, ActionIds.WorkingTreeToggleNoVerify, ActionIds.WorkingTreeToggleAmend]),
        new("<space>te", "$EDITOR", [ActionIds.ToolCommitEditor]),
    ];

    private static readonly ImmutableArray<KeyHint> MessageEditing =
    [
        new("Esc", "stop writing", [ActionIds.Cancel]),
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

    private static readonly ImmutableArray<KeyHint> ConflictVersions =
    [
        new("n/N", "conflict", [ActionIds.ConflictEditorNext, ActionIds.ConflictEditorPrevious]),
        new("<space>a", "take pane", [ActionIds.ConflictEditorTake]),
        new("<space>u", "untake", [ActionIds.ConflictEditorUntake]),
        new("o/t/b", "ours/theirs/base", [
            ActionIds.ConflictEditorOurs,
            ActionIds.ConflictEditorTheirs,
            ActionIds.ConflictEditorBase,
        ]),
        new("v", "select lines", [ActionIds.VisualToggle]),
        new("e", "edit in result", [ActionIds.ConflictEditorEdit]),
        new("u", "undo", [ActionIds.ConflictEditorUndo]),
        new("Tab", "pane", [ActionIds.PaneNext]),
        new("Enter", "save", [ActionIds.ConflictEditorSave]),
        new("Esc", "close", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> ConflictVisual =
    [
        new("<space>a", "take lines", [ActionIds.ConflictEditorTake]),
        new("<space>u", "untake lines", [ActionIds.ConflictEditorUntake]),
        new("j/k", "extend", [ActionIds.ListDown, ActionIds.ListUp]),
        new("v", "end selection", [ActionIds.VisualToggle]),
    ];

    private static readonly ImmutableArray<KeyHint> ConflictResult =
    [
        new("n/N", "conflict", [ActionIds.ConflictEditorNext, ActionIds.ConflictEditorPrevious]),
        new("<space>u", "untake", [ActionIds.ConflictEditorUntake]),
        new("i", "type", [ActionIds.ConflictEditorEdit]),
        new("u", "undo", [ActionIds.ConflictEditorUndo]),
        new("Tab", "pane", [ActionIds.PaneNext]),
        new("Enter", "save", [ActionIds.ConflictEditorSave]),
        new("Esc", "close", [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> ConflictEdit =
    [
        new("Esc", "keep", [ActionIds.Cancel]),
        new("Ctrl-g", "keep", [ActionIds.ConflictEditorEditDone]),
        new("Enter", "new line", [ActionIds.ConflictEditorEditNewLine]),
    ];

    private static readonly ImmutableArray<KeyHint> RebaseMessage =
    [
        new("Ctrl-g", "keep", [ActionIds.RebaseEditorMessageSave]),
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

    private static readonly ImmutableArray<KeyHint> RepeatContext =
    [
        new("+/-", "more/fewer context", [ActionIds.DiffMoreContext, ActionIds.DiffLessContext]),
        new("Esc", "done", [ActionIds.RepeatEnd]),
    ];

    private static readonly ImmutableArray<KeyHint> RepeatResize =
    [
        new(">/<", "wider/narrower", [ActionIds.PaneWider, ActionIds.PaneNarrower]),
        new("+/-", "taller/shorter", [ActionIds.PaneTaller, ActionIds.PaneShorter]),
        new("=", "reset", [ActionIds.PaneResetSizes]),
        new("Esc", "done", [ActionIds.RepeatEnd]),
    ];

    /// <summary>The only hint line of a pane while its repeat mode is on (#758).</summary>
    /// <param name="kind">What the mode repeats.</param>
    public static IReadOnlyList<KeyHint> ForRepeat(RepeatKind kind) =>
        kind == RepeatKind.Context ? RepeatContext : RepeatResize;

    /// <summary>The hints of the focused <paramref name="pane"/>; file content has its own.</summary>
    /// <param name="pane">The focused pane.</param>
    /// <param name="showsFileContent">Whether the diff pane shows file content.</param>
    /// <param name="onConflict">Whether the working-tree selection is a conflicted file: the Unstaged list and its
    /// diff then lead with the conflict editor's keys (#742).</param>
    /// <param name="onRefusedConflict">Whether the selection is a conflicted file the editor does not take
    /// (delete/modify and the like): the same two panes then lead with the mergetool key instead.</param>
    public static IReadOnlyList<KeyHint> For(
        PaneKind pane,
        bool showsFileContent,
        bool onConflict = false,
        bool onRefusedConflict = false)
    {
        if (onRefusedConflict && pane == PaneKind.WorkingUnstaged)
        {
            return WorkingUnstagedRefused;
        }

        if (onRefusedConflict && pane == PaneKind.WorkingUnstagedDiff)
        {
            return WorkingUnstagedDiffRefused;
        }

        if (onConflict && pane == PaneKind.WorkingUnstaged)
        {
            return WorkingUnstagedConflict;
        }

        if (onConflict && pane == PaneKind.WorkingUnstagedDiff)
        {
            return WorkingUnstagedDiffConflict;
        }

        return pane == PaneKind.Diff && showsFileContent ? FileContent : For(pane);
    }

    /// <summary>
    /// <paramref name="hints"/> with <c>&lt;space&gt;to conflict editor</c> right after help, commands and quit: a
    /// merge, rebase, cherry-pick or revert is stopped on a conflict, and the key works from every pane (#742). Hints
    /// that already lead with the editor are returned as they are.
    /// </summary>
    /// <param name="hints">The hints of the focused pane.</param>
    public static IReadOnlyList<KeyHint> WithConflictEditor(IReadOnlyList<KeyHint> hints) =>
        WithConflictHint(hints, ConflictEditorHint, ConflictEditorLabel);

    /// <summary>
    /// <paramref name="hints"/> with <c>&lt;space&gt;tm mergetool</c> in the same place, for a stopped operation
    /// whose conflicted files are all ones the editor does not take (#742).
    /// </summary>
    /// <param name="hints">The hints of the focused pane.</param>
    public static IReadOnlyList<KeyHint> WithConflictMergetool(IReadOnlyList<KeyHint> hints) =>
        WithConflictHint(hints, ConflictMergetoolHint, MergetoolLabel);

    private static IReadOnlyList<KeyHint> WithConflictHint(IReadOnlyList<KeyHint> hints, KeyHint lead, string label)
    {
        if (hints.Any(hint => hint.Description == label))
        {
            return hints;
        }

        return [.. hints.Take(Pinned.Length), lead, .. hints.Skip(Pinned.Length)];
    }

    /// <summary>The hints of the detail pane while its Changes tab is selected.</summary>
    public static IReadOnlyList<KeyHint> ForDetailChanges() => DetailChanges;

    /// <summary>The hints of the graph while a commit is marked for a comparison (#636).</summary>
    public static IReadOnlyList<KeyHint> ForGraphComparing() => GraphComparing;

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

    /// <summary>The hints while the conflict editor's cursor is in a version pane (base, ours or theirs).</summary>
    public static IReadOnlyList<KeyHint> ForConflictVersions() => ConflictVersions;

    /// <summary>The hints while lines are selected in a version pane of the conflict editor.</summary>
    public static IReadOnlyList<KeyHint> ForConflictVisual() => ConflictVisual;

    /// <summary>The hints while the conflict editor's cursor is in the result pane (Normal mode).</summary>
    public static IReadOnlyList<KeyHint> ForConflictResult() => ConflictResult;

    /// <summary>The hints while the conflict editor's result is typed in (Insert mode).</summary>
    public static IReadOnlyList<KeyHint> ForConflictEdit() => ConflictEdit;

    /// <summary>The hints while a reword's message is edited in the rebase todo editor.</summary>
    public static IReadOnlyList<KeyHint> ForRebaseMessage() => RebaseMessage;

    /// <summary>The hints while the settings screen is open.</summary>
    public static IReadOnlyList<KeyHint> ForSettings() => Settings;

    /// <summary>The hints while the repository picker is open.</summary>
    public static IReadOnlyList<KeyHint> ForPicker() => Picker;

    /// <summary>The hints in the footer of the commands popup.</summary>
    public static IReadOnlyList<KeyHint> ForPalette() => Palette;
}
