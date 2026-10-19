using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// The default bindings, written as data. They mirror the keymap table in <c>docs/ux.md</c> (a test checks
/// that); user config in M4 builds a <see cref="KeyBindingTable"/> from these plus overrides.
/// </summary>
public static class DefaultKeymap
{
    public const string ListsView = "Lists";
    public const string GraphView = "Graph";
    public const string DetailView = "Detail";
    public const string SidebarView = "Sidebar";
    public const string DiffView = "Diff";
    public const string WorkingUnstagedView = "WorkingUnstaged";
    public const string WorkingStagedView = "WorkingStaged";
    public const string WorkingUnstagedDiffView = "WorkingUnstagedDiff";
    public const string WorkingStagedDiffView = "WorkingStagedDiff";
    public const string WorkingMessageView = "WorkingMessage";
    public const string PickerView = "Picker";
    public const string PaletteView = "Palette";
    public const string ContextMenuView = "ContextMenu";
    public const string HelpView = "Help";
    public const string MessagesView = "Messages";
    public const string ConfirmView = "Confirm";
    public const string ReflogView = "Reflog";
    public const string RebaseEditorView = "RebaseEditor";
    public const string SettingsView = "Settings";

    private static readonly KeyMode[] Navigation = [KeyMode.Normal, KeyMode.Visual];
    private static readonly KeyMode[] NormalOnly = [KeyMode.Normal];
    private static readonly KeyMode[] TextEntry = [KeyMode.Normal, KeyMode.Visual, KeyMode.Insert];

    /// <summary>Views that inherit from <c>Lists</c>; the others inherit from <c>Global</c> only.</summary>
    public static IReadOnlyDictionary<string, string> ViewParents { get; } = new Dictionary<string, string>
    {
        [GraphView] = ListsView,
        [DetailView] = ListsView,
        [SidebarView] = ListsView,
        [DiffView] = ListsView,
        [WorkingUnstagedView] = ListsView,
        [WorkingStagedView] = ListsView,
        [WorkingUnstagedDiffView] = ListsView,
        [WorkingStagedDiffView] = ListsView,
        [ListsView] = KeyBindingTable.GlobalView,
        [PickerView] = KeyBindingTable.GlobalView,
        [PaletteView] = KeyBindingTable.GlobalView,
        [ContextMenuView] = KeyBindingTable.GlobalView,
        [HelpView] = ListsView,
        [MessagesView] = ListsView,
        [ConfirmView] = KeyBindingTable.GlobalView,
        [ReflogView] = ListsView,
        [RebaseEditorView] = ListsView,
        [SettingsView] = ListsView,
    };

    public static KeyBindingTable Create() => new(CreateBindings(), ViewParents);

    public static IReadOnlyList<KeyBinding> CreateBindings()
    {
        var bindings = new List<KeyBinding>();
        AddGlobal(bindings);
        AddLists(bindings);
        AddViews(bindings);
        AddSettingsKeys(bindings);
        AddWorkingTreeLists(bindings);
        AddWorkingTreeDiffs(bindings);
        AddWorkingMessage(bindings);
        AddPicker(bindings);
        AddPalette(bindings);
        AddContextMenu(bindings);
        AddModals(bindings);
        return bindings;
    }

    private static void AddGlobal(List<KeyBinding> bindings)
    {
        const string global = KeyBindingTable.GlobalView;
        Add(bindings, Navigation, global, "?", ActionIds.HelpShow);
        Add(bindings, NormalOnly, global, ":", ActionIds.PaletteOpen);
        Add(bindings, TextEntry, global, "Ctrl-p", ActionIds.PaletteOpen);
        Add(bindings, Navigation, global, "q", ActionIds.AppQuit);
        Add(bindings, NormalOnly, global, "Tab", ActionIds.PaneNext);
        Add(bindings, NormalOnly, global, "Shift-Tab", ActionIds.PanePrevious);
        Add(bindings, NormalOnly, global, "gt", ActionIds.TabNext);
        Add(bindings, NormalOnly, global, "gT", ActionIds.TabPrevious);
        for (var number = 1; number <= 9; number++)
        {
            Add(bindings, TextEntry, global, $"Alt-{number}", ActionIds.TabGoTo(number));
        }

        Add(bindings, TextEntry, global, "Ctrl-t", ActionIds.PickerOpen);
        Add(bindings, NormalOnly, global, "<space>o", ActionIds.PickerOpen);
        Add(bindings, NormalOnly, global, "Ctrl-w c", ActionIds.TabClose);
        Add(bindings, NormalOnly, global, "Ctrl-w r", ActionIds.TabRename);
        Add(bindings, NormalOnly, global, "<space>p", ActionIds.RepositoryPin);
        Add(bindings, NormalOnly, global, "Ctrl-w h", ActionIds.PaneFocusLeft);
        Add(bindings, NormalOnly, global, "Ctrl-w j", ActionIds.PaneFocusDown);
        Add(bindings, NormalOnly, global, "Ctrl-w k", ActionIds.PaneFocusUp);
        Add(bindings, NormalOnly, global, "Ctrl-w l", ActionIds.PaneFocusRight);
        Add(bindings, NormalOnly, global, "Ctrl-w w", ActionIds.PaneNext);
        Add(bindings, NormalOnly, global, "Ctrl-h", ActionIds.PaneFocusLeft);
        Add(bindings, NormalOnly, global, "Ctrl-Backspace", ActionIds.PaneFocusLeft);
        Add(bindings, NormalOnly, global, "Ctrl-j", ActionIds.PaneFocusDown);
        Add(bindings, NormalOnly, global, "Ctrl-k", ActionIds.PaneFocusUp);
        Add(bindings, NormalOnly, global, "Ctrl-l", ActionIds.PaneFocusRight);
        Add(bindings, TextEntry, global, "Ctrl-^", ActionIds.TabLastActive);
        Add(bindings, NormalOnly, global, "<space><Tab>", ActionIds.TabLastActive);
        Add(bindings, NormalOnly, global, "Ctrl-w >", ActionIds.PaneWider);
        Add(bindings, NormalOnly, global, "Ctrl-w <lt>", ActionIds.PaneNarrower);
        Add(bindings, NormalOnly, global, "Ctrl-w +", ActionIds.PaneTaller);
        Add(bindings, NormalOnly, global, "Ctrl-w -", ActionIds.PaneShorter);
        Add(bindings, NormalOnly, global, "Ctrl-w =", ActionIds.PaneResetSizes);
        Add(bindings, NormalOnly, global, "<space>z", ActionIds.PaneZoom);
        Add(bindings, NormalOnly, global, "<space>x", ActionIds.StatusDismiss);
        Add(bindings, NormalOnly, global, "<space>m", ActionIds.StatusHistory);
        Add(bindings, NormalOnly, global, "<space>,", ActionIds.SettingsOpen);
        Add(bindings, NormalOnly, global, "<space>s", ActionIds.WorkingTreeToggle);
        Add(bindings, NormalOnly, global, "<space>F", ActionIds.RemoteFetch);
        Add(bindings, NormalOnly, global, "<space>L", ActionIds.RemotePull);
        Add(bindings, NormalOnly, global, "<space>P", ActionIds.RemotePush);
        Add(bindings, NormalOnly, global, "<space>Rf", ActionIds.RemotePushForce);
        Add(bindings, NormalOnly, global, "<space>RD", ActionIds.RemoteDeleteBranch);
        Add(bindings, NormalOnly, global, "<space>C", ActionIds.RemoteCancel);
        Add(bindings, NormalOnly, global, "<space>Ra", ActionIds.RemoteAdd);
        Add(bindings, NormalOnly, global, "<space>Rd", ActionIds.RemoteRemove);
        Add(bindings, NormalOnly, global, "<space>Rn", ActionIds.RemoteRename);
        Add(bindings, NormalOnly, global, "<space>Ru", ActionIds.RemoteSetUrl);
        Add(bindings, NormalOnly, global, "<space>Ss", ActionIds.StashPush);
        Add(bindings, NormalOnly, global, "<space>Su", ActionIds.StashPushUntracked);
        Add(bindings, NormalOnly, global, "<space>gc", ActionIds.OperationContinue);
        Add(bindings, NormalOnly, global, "<space>ga", ActionIds.OperationAbort);
        Add(bindings, NormalOnly, global, "<space>gk", ActionIds.OperationSkip);
        Add(bindings, [.. Enum.GetValues<KeyMode>()], global, "Esc", ActionIds.Cancel);
    }

    private static void AddLists(List<KeyBinding> bindings)
    {
        Add(bindings, Navigation, ListsView, "j", ActionIds.ListDown);
        Add(bindings, Navigation, ListsView, "k", ActionIds.ListUp);
        Add(bindings, Navigation, ListsView, "gg", ActionIds.ListTop);
        Add(bindings, Navigation, ListsView, "G", ActionIds.ListBottom);
        Add(bindings, NormalOnly, ListsView, "/", ActionIds.SearchOpen);
        Add(bindings, NormalOnly, ListsView, "n", ActionIds.SearchNext);
        Add(bindings, NormalOnly, ListsView, "N", ActionIds.SearchPrevious);
        Add(bindings, Navigation, ListsView, "Ctrl-d", ActionIds.ListHalfPageDown);
        Add(bindings, Navigation, ListsView, "Ctrl-u", ActionIds.ListHalfPageUp);
        Add(bindings, Navigation, ListsView, "Down", ActionIds.ListDown);
        Add(bindings, Navigation, ListsView, "Up", ActionIds.ListUp);
        Add(bindings, Navigation, ListsView, "PageDown", ActionIds.ListHalfPageDown);
        Add(bindings, Navigation, ListsView, "PageUp", ActionIds.ListHalfPageUp);
        Add(bindings, Navigation, ListsView, "v", ActionIds.VisualToggle);
        Add(bindings, [KeyMode.Visual], ListsView, "y", ActionIds.VisualCopy);
    }

    private static void AddViews(List<KeyBinding> bindings)
    {
        Add(bindings, NormalOnly, GraphView, "<space>h", ActionIds.GraphJumpToHead);
        Add(bindings, NormalOnly, GraphView, "<space>f", ActionIds.GraphJumpToRef);
        Add(bindings, NormalOnly, GraphView, "<space>j", ActionIds.QuickJump);
        Add(bindings, NormalOnly, GraphView, "Enter", ActionIds.GraphOpenDetails);
        Add(bindings, NormalOnly, GraphView, "y", ActionIds.GraphCopyHash);
        Add(bindings, NormalOnly, GraphView, "<space>r", ActionIds.RepositoryRefresh);
        Add(bindings, NormalOnly, GraphView, "<space>td", ActionIds.ToolDifftool);
        Add(bindings, NormalOnly, GraphView, "<space>tm", ActionIds.ToolMergetool);
        Add(bindings, NormalOnly, GraphView, "<space>te", ActionIds.ToolCommitEditor);
        AddBottomTabKeys(bindings, GraphView);
        AddBranchKeys(bindings, GraphView);
        AddBranchKeys(bindings, SidebarView);
        AddTagKeys(bindings, GraphView);
        AddTagKeys(bindings, SidebarView);
        AddResetKeys(bindings, GraphView);
        Add(bindings, NormalOnly, GraphView, "<space>l", ActionIds.ReflogOpen);
        Add(bindings, NormalOnly, GraphView, "<space>.", ActionIds.MenuOpenForRow);
        Add(bindings, NormalOnly, SidebarView, "<space>.", ActionIds.MenuOpenForRow);
        Add(bindings, NormalOnly, SidebarView, "<space>l", ActionIds.ReflogOpen);
        AddReflogKeys(bindings);
        AddRebaseEditorKeys(bindings);
        AddResetKeys(bindings, SidebarView);
        AddMergeKeys(bindings, GraphView);
        AddMergeKeys(bindings, SidebarView);
        AddPickKeys(bindings, GraphView, Navigation);
        AddPickKeys(bindings, SidebarView, NormalOnly);

        Add(bindings, NormalOnly, DetailView, "Enter", ActionIds.DetailOpenDiff);
        Add(bindings, NormalOnly, DetailView, "l", ActionIds.DetailOpenDiff);
        Add(bindings, NormalOnly, DetailView, "<space>r", ActionIds.DetailRetry);
        Add(bindings, NormalOnly, DetailView, "y", ActionIds.DetailCopyPath);
        Add(bindings, NormalOnly, DetailView, "<space>j", ActionIds.QuickJump);
        Add(bindings, NormalOnly, DetailView, "h", ActionIds.DetailCollapse);
        Add(bindings, NormalOnly, DetailView, "/", ActionIds.DetailFilter);
        Add(bindings, NormalOnly, DetailView, "Esc", ActionIds.NavigateBack);
        AddBottomTabKeys(bindings, DetailView);

        Add(bindings, NormalOnly, SidebarView, "l", ActionIds.SidebarExpand);
        Add(bindings, NormalOnly, SidebarView, "Enter", ActionIds.SidebarActivate);
        Add(bindings, NormalOnly, SidebarView, "h", ActionIds.SidebarCollapse);
        Add(bindings, NormalOnly, SidebarView, "/", ActionIds.SidebarFilter);
        Add(bindings, NormalOnly, SidebarView, "y", ActionIds.SidebarCopy);
        Add(bindings, NormalOnly, SidebarView, "<space>Sa", ActionIds.StashApply);
        Add(bindings, NormalOnly, SidebarView, "<space>Sp", ActionIds.StashPop);
        Add(bindings, NormalOnly, SidebarView, "<space>Sd", ActionIds.StashDrop);
        Add(bindings, NormalOnly, SidebarView, "<space>wa", ActionIds.WorktreeAdd);
        Add(bindings, NormalOnly, SidebarView, "<space>wd", ActionIds.WorktreeRemove);
        Add(bindings, NormalOnly, SidebarView, "<space>wl", ActionIds.WorktreeLock);
        Add(bindings, NormalOnly, SidebarView, "<space>wu", ActionIds.WorktreeUnlock);
        Add(bindings, NormalOnly, SidebarView, "<space>ws", ActionIds.WorktreeSwitch);
        Add(bindings, NormalOnly, SidebarView, "<space>wt", ActionIds.WorktreeOpen);
        Add(bindings, NormalOnly, SidebarView, "<space>wp", ActionIds.WorktreePrune);

        Add(bindings, NormalOnly, DiffView, "]c", ActionIds.DiffNextHunk);
        Add(bindings, NormalOnly, DiffView, "[c", ActionIds.DiffPreviousHunk);
        Add(bindings, NormalOnly, DiffView, "]f", ActionIds.DiffNextFile);
        Add(bindings, NormalOnly, DiffView, "[f", ActionIds.DiffPreviousFile);
        Add(bindings, NormalOnly, DiffView, "Esc", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, DiffView, "h", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, DiffView, "<space>vw", ActionIds.DiffToggleWrap);
        Add(bindings, NormalOnly, DiffView, "<space>vl", ActionIds.DiffLoadAnyway);
        Add(bindings, NormalOnly, DiffView, "+", ActionIds.DiffMoreContext);
        Add(bindings, NormalOnly, DiffView, "-", ActionIds.DiffLessContext);
        Add(bindings, NormalOnly, DiffView, "<space>vf", ActionIds.DiffToggleWholeFile);
        Add(bindings, NormalOnly, DiffView, "<space>vi", ActionIds.DiffToggleIgnoreWhitespace);
        Add(bindings, NormalOnly, DiffView, "<space>vs", ActionIds.DiffToggleSideBySide);
        Add(bindings, NormalOnly, DiffView, "/", ActionIds.DiffSearchOpen);
        Add(bindings, NormalOnly, DiffView, "n", ActionIds.DiffSearchNext);
        Add(bindings, NormalOnly, DiffView, "N", ActionIds.DiffSearchPrevious);
        Add(bindings, NormalOnly, DiffView, "y", ActionIds.DiffCopyLine);
        Add(bindings, NormalOnly, DiffView, "Y", ActionIds.DiffCopyFile);
        Add(bindings, NormalOnly, DiffView, "<space>j", ActionIds.QuickJump);
    }

    // The branch operations act on the selected commit (graph) or the selected row (sidebar).
    private static void AddBranchKeys(List<KeyBinding> bindings, string view)
    {
        Add(bindings, NormalOnly, view, "<space>bc", ActionIds.BranchCreate);
        Add(bindings, NormalOnly, view, "<space>bC", ActionIds.BranchCreateOnly);
        Add(bindings, NormalOnly, view, "<space>bs", ActionIds.BranchSwitch);
        Add(bindings, NormalOnly, view, "<space>br", ActionIds.BranchRename);
        Add(bindings, NormalOnly, view, "<space>bd", ActionIds.BranchDelete);
        Add(bindings, NormalOnly, view, "<space>bu", ActionIds.BranchSetUpstream);
        Add(bindings, NormalOnly, view, "<space>bU", ActionIds.BranchUnsetUpstream);
    }

    // The tag operations act on the selected commit (graph) or the selected row (sidebar).
    private static void AddTagKeys(List<KeyBinding> bindings, string view)
    {
        Add(bindings, NormalOnly, view, "<space>Tc", ActionIds.TagCreate);
        Add(bindings, NormalOnly, view, "<space>Td", ActionIds.TagDelete);
        Add(bindings, NormalOnly, view, "<space>Tp", ActionIds.TagPush);
        Add(bindings, NormalOnly, view, "<space>TD", ActionIds.TagDeleteRemote);
    }

    // Merge integrates the selected commit, branch or tag into the current branch.
    private static void AddMergeKeys(List<KeyBinding> bindings, string view)
    {
        Add(bindings, NormalOnly, view, "<space>gm", ActionIds.MergeBranch);
        Add(bindings, NormalOnly, view, "<space>gM", ActionIds.MergeBranchNoFastForward);
        Add(bindings, NormalOnly, view, "<space>gs", ActionIds.MergeBranchSquash);
    }

    // Cherry-pick and revert act on the selected commit; in the graph also on a Visual range.
    private static void AddPickKeys(List<KeyBinding> bindings, string view, KeyMode[] modes)
    {
        Add(bindings, modes, view, "<space>gp", ActionIds.PickCherry);
        Add(bindings, modes, view, "<space>gv", ActionIds.PickRevert);
    }

    // Reset moves the current branch to the selected commit, branch or tag.
    private static void AddResetKeys(List<KeyBinding> bindings, string view)
    {
        Add(bindings, NormalOnly, view, "<space>gx", ActionIds.ResetBranch);
        Add(bindings, NormalOnly, view, "<space>gr", ActionIds.RebaseOnto);
        Add(bindings, NormalOnly, view, "<space>gi", ActionIds.RebaseInteractive);
    }

    // The reflog list acts on the entry under the cursor; the actions are the graph's, the controller aims them.
    private static void AddReflogKeys(List<KeyBinding> bindings)
    {
        Add(bindings, NormalOnly, ReflogView, "Enter", ActionIds.ReflogSelectCommit);
        Add(bindings, NormalOnly, ReflogView, "h", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, ReflogView, "Esc", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, ReflogView, "<space>r", ActionIds.RepositoryRefresh);
        Add(bindings, NormalOnly, ReflogView, "<space>l", ActionIds.ReflogOpen);
        Add(bindings, NormalOnly, ReflogView, "/", ActionIds.SearchOpen);
        Add(bindings, NormalOnly, ReflogView, "n", ActionIds.SearchNext);
        Add(bindings, NormalOnly, ReflogView, "N", ActionIds.SearchPrevious);
        Add(bindings, NormalOnly, ReflogView, "<space>gx", ActionIds.ResetBranch);
        Add(bindings, NormalOnly, ReflogView, "<space>bc", ActionIds.BranchCreate);
        Add(bindings, NormalOnly, ReflogView, "<space>bs", ActionIds.BranchSwitch);
    }

    // The todo editor of an interactive rebase is a modal list like the settings screen: the single letters are git's
    // own todo words (pick, reword, squash, fixup, drop), keys of the list and not buffer commands. In Insert mode the
    // reword's message editor takes text; only Enter, Esc and the Alt-Enter/Ctrl-g accept act.
    private static void AddRebaseEditorKeys(List<KeyBinding> bindings)
    {
        const string editor = RebaseEditorView;
        Add(bindings, NormalOnly, editor, "p", ActionIds.RebaseEditorPick);
        Add(bindings, NormalOnly, editor, "r", ActionIds.RebaseEditorReword);
        Add(bindings, NormalOnly, editor, "s", ActionIds.RebaseEditorSquash);
        Add(bindings, NormalOnly, editor, "f", ActionIds.RebaseEditorFixup);
        Add(bindings, NormalOnly, editor, "d", ActionIds.RebaseEditorDrop);
        Add(bindings, NormalOnly, editor, "K", ActionIds.RebaseEditorMoveUp);
        Add(bindings, NormalOnly, editor, "J", ActionIds.RebaseEditorMoveDown);
        Add(bindings, NormalOnly, editor, "Alt-k", ActionIds.RebaseEditorMoveUp);
        Add(bindings, NormalOnly, editor, "Alt-j", ActionIds.RebaseEditorMoveDown);
        Add(bindings, NormalOnly, editor, "Enter", ActionIds.RebaseEditorStart);
        Add(bindings, NormalOnly, editor, "h", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, editor, "Esc", ActionIds.NavigateBack);
        Add(bindings, [KeyMode.Insert], editor, "Enter", ActionIds.RebaseEditorMessageNewLine);
        Add(bindings, [KeyMode.Insert], editor, "Alt-Enter", ActionIds.RebaseEditorMessageSave);
        Add(bindings, [KeyMode.Insert], editor, "Ctrl-g", ActionIds.RebaseEditorMessageSave);
    }

    // The settings screen: the list motions come from Lists; these edit the setting under the cursor. They are keys
    // of a modal list, not buffer commands, so the single letters do not clash with the Normal-mode rule of the panes.
    private static void AddSettingsKeys(List<KeyBinding> bindings)
    {
        Add(bindings, NormalOnly, SettingsView, "Enter", ActionIds.SettingsActivate);
        Add(bindings, NormalOnly, SettingsView, "l", ActionIds.SettingsActivate);
        Add(bindings, NormalOnly, SettingsView, "h", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, SettingsView, "Esc", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, SettingsView, "x", ActionIds.SettingsToggle);
        Add(bindings, NormalOnly, SettingsView, "/", ActionIds.SettingsFilter);
        Add(bindings, NormalOnly, SettingsView, "a", ActionIds.SettingsAdd);
        Add(bindings, NormalOnly, SettingsView, "d", ActionIds.SettingsRemove);
        Add(bindings, NormalOnly, SettingsView, "J", ActionIds.SettingsMoveDown);
        Add(bindings, NormalOnly, SettingsView, "K", ActionIds.SettingsMoveUp);
        Add(bindings, NormalOnly, SettingsView, "+", ActionIds.SettingsIncrease);
        Add(bindings, NormalOnly, SettingsView, "-", ActionIds.SettingsDecrease);
    }

    // The two file lists of the working-tree view share their keys; stage and unstage are side-specific; discard is unstaged-only.
    private static void AddWorkingTreeLists(List<KeyBinding> bindings)
    {
        foreach (var view in (string[])[WorkingUnstagedView, WorkingStagedView])
        {
            Add(bindings, NormalOnly, view, "<space>r", ActionIds.RepositoryRefresh);
            Add(bindings, NormalOnly, view, "y", ActionIds.WorkingTreeCopyPath);
            Add(bindings, NormalOnly, view, "h", ActionIds.NavigateBack);
            Add(bindings, NormalOnly, view, "Esc", ActionIds.NavigateBack);
            Add(bindings, NormalOnly, view, "<space>c", ActionIds.WorkingTreeWriteMessage);
            Add(bindings, NormalOnly, view, "<space>te", ActionIds.ToolCommitEditor);
            Add(bindings, NormalOnly, view, "Enter", ActionIds.WorkingTreeOpenDiff);
            Add(bindings, NormalOnly, view, "l", ActionIds.WorkingTreeOpenDiff);
            Add(bindings, NormalOnly, view, "<space>j", ActionIds.QuickJump);
        }

        // Stage, unstage and discard also act on a Visual range of files.
        Add(bindings, Navigation, WorkingUnstagedView, "<space>a", ActionIds.WorkingTreeStage);
        Add(bindings, NormalOnly, WorkingUnstagedView, "<space>A", ActionIds.WorkingTreeStageAll);
        Add(bindings, Navigation, WorkingUnstagedView, "<space>d", ActionIds.WorkingTreeDiscard);
        Add(bindings, Navigation, WorkingStagedView, "<space>u", ActionIds.WorkingTreeUnstage);
        Add(bindings, NormalOnly, WorkingStagedView, "<space>U", ActionIds.WorkingTreeUnstageAll);
    }

    // The two sides of the working-tree diff share the keys of the history diff; the side-specific ones (stage, unstage, discard) are added below.
    private static void AddWorkingTreeDiffs(List<KeyBinding> bindings)
    {
        foreach (var view in (string[])[WorkingUnstagedDiffView, WorkingStagedDiffView])
        {
            Add(bindings, NormalOnly, view, "<space>r", ActionIds.RepositoryRefresh);
            Add(bindings, NormalOnly, view, "]c", ActionIds.DiffNextHunk);
            Add(bindings, NormalOnly, view, "[c", ActionIds.DiffPreviousHunk);
            Add(bindings, NormalOnly, view, "]f", ActionIds.DiffNextFile);
            Add(bindings, NormalOnly, view, "[f", ActionIds.DiffPreviousFile);
            Add(bindings, NormalOnly, view, "Esc", ActionIds.NavigateBack);
            Add(bindings, NormalOnly, view, "h", ActionIds.NavigateBack);
            Add(bindings, NormalOnly, view, "<space>vw", ActionIds.DiffToggleWrap);
            Add(bindings, NormalOnly, view, "<space>vl", ActionIds.DiffLoadAnyway);
            Add(bindings, NormalOnly, view, "+", ActionIds.DiffMoreContext);
            Add(bindings, NormalOnly, view, "-", ActionIds.DiffLessContext);
            Add(bindings, NormalOnly, view, "<space>vf", ActionIds.DiffToggleWholeFile);
            Add(bindings, NormalOnly, view, "<space>vi", ActionIds.DiffToggleIgnoreWhitespace);
            Add(bindings, NormalOnly, view, "<space>vs", ActionIds.DiffToggleSideBySide);
            Add(bindings, NormalOnly, view, "/", ActionIds.DiffSearchOpen);
            Add(bindings, NormalOnly, view, "n", ActionIds.DiffSearchNext);
            Add(bindings, NormalOnly, view, "N", ActionIds.DiffSearchPrevious);
            Add(bindings, NormalOnly, view, "y", ActionIds.DiffCopyLine);
            Add(bindings, NormalOnly, view, "Y", ActionIds.DiffCopyFile);
            Add(bindings, NormalOnly, view, "<space>j", ActionIds.QuickJump);
            Add(bindings, NormalOnly, view, "<space>c", ActionIds.WorkingTreeWriteMessage);
            Add(bindings, NormalOnly, view, "<space>te", ActionIds.ToolCommitEditor);
        }

        // In the diff the same keys act on the hunk under the cursor, or on the Visual lines (also bound in Visual mode).
        Add(bindings, Navigation, WorkingUnstagedDiffView, "<space>a", ActionIds.WorkingTreeStage);
        Add(bindings, Navigation, WorkingUnstagedDiffView, "<space>d", ActionIds.WorkingTreeDiscard);
        Add(bindings, Navigation, WorkingStagedDiffView, "<space>u", ActionIds.WorkingTreeUnstage);
    }

    // The message box: Normal mode is its own view of the keymap, Insert mode types into the editor (its editing keys are not in
    // the keymap, like the prompt editing keys). The option toggles work from every pane of the working-tree view, Insert included.
    private static void AddWorkingMessage(List<KeyBinding> bindings)
    {
        const string message = WorkingMessageView;
        Add(bindings, NormalOnly, message, "i", ActionIds.WorkingTreeEditMessage);
        Add(bindings, NormalOnly, message, "Enter", ActionIds.WorkingTreeEditMessage);
        Add(bindings, NormalOnly, message, "<space>c", ActionIds.WorkingTreeCommit);
        Add(bindings, NormalOnly, message, "<space>te", ActionIds.ToolCommitEditor);
        Add(bindings, NormalOnly, message, "<space>r", ActionIds.RepositoryRefresh);
        Add(bindings, NormalOnly, message, "Esc", ActionIds.NavigateBack);
        Add(bindings, NormalOnly, message, "h", ActionIds.NavigateBack);
        Add(bindings, [KeyMode.Insert], message, "Enter", ActionIds.WorkingTreeMessageNewLine);
        Add(bindings, [KeyMode.Insert], message, "Alt-Enter", ActionIds.WorkingTreeCommit);
        Add(bindings, [KeyMode.Insert], message, "Ctrl-g", ActionIds.WorkingTreeCommit);
        foreach (var view in (string[])[WorkingUnstagedView, WorkingStagedView, WorkingUnstagedDiffView, WorkingStagedDiffView, message])
        {
            Add(bindings, TextEntry, view, "Alt-s", ActionIds.WorkingTreeToggleSignOff);
            Add(bindings, TextEntry, view, "Alt-n", ActionIds.WorkingTreeToggleNoVerify);
            Add(bindings, TextEntry, view, "Alt-a", ActionIds.WorkingTreeToggleAmend);
            Add(bindings, NormalOnly, view, "<space>1", ActionIds.WorkingTreeFocusUnstaged);
            Add(bindings, NormalOnly, view, "<space>2", ActionIds.WorkingTreeFocusStaged);
            Add(bindings, NormalOnly, view, "<space>3", ActionIds.WorkingTreeFocusDiff);
            Add(bindings, NormalOnly, view, "<space>4", ActionIds.WorkingTreeFocusMessage);
        }
    }

    // The graph feeds the bottom pane with its selection, so the tabs switch from there too.
    private static void AddBottomTabKeys(List<KeyBinding> bindings, string view)
    {
        Add(bindings, NormalOnly, view, "]", ActionIds.DetailNextTab);
        Add(bindings, NormalOnly, view, "[", ActionIds.DetailPreviousTab);
        Add(bindings, NormalOnly, view, "gd", ActionIds.DetailTabDetails);
        Add(bindings, NormalOnly, view, "gc", ActionIds.DetailTabChanges);
        Add(bindings, NormalOnly, view, "gf", ActionIds.DetailTabFiles);
    }

    private static void AddPicker(List<KeyBinding> bindings)
    {
        Add(bindings, [KeyMode.Insert], PickerView, "Up", ActionIds.PickerPrevious);
        Add(bindings, [KeyMode.Insert], PickerView, "Down", ActionIds.PickerNext);
        Add(bindings, [KeyMode.Insert], PickerView, "Ctrl-j", ActionIds.PickerNext);
        Add(bindings, [KeyMode.Insert], PickerView, "Ctrl-k", ActionIds.PickerPrevious);
        Add(bindings, [KeyMode.Insert], PickerView, "Enter", ActionIds.PickerAccept);
        Add(bindings, [KeyMode.Insert], PickerView, "Ctrl-l", ActionIds.PickerAccept);
        Add(bindings, [KeyMode.Insert], PickerView, "Tab", ActionIds.PickerCompletePath);
        Add(bindings, [KeyMode.Insert], PickerView, "Right", ActionIds.PickerCompletePath);
        Add(bindings, [KeyMode.Insert], PickerView, "Alt-p", ActionIds.PickerPin);
        Add(bindings, [KeyMode.Insert], PickerView, "Alt-d", ActionIds.PickerRemoveRecent);
        Add(bindings, [KeyMode.Insert], PickerView, "Alt-c", ActionIds.PickerClone);
        Add(bindings, [KeyMode.Insert], PickerView, "Alt-i", ActionIds.PickerInit);
    }

    private static void AddPalette(List<KeyBinding> bindings)
    {
        Add(bindings, [KeyMode.Command], PaletteView, "Up", ActionIds.PalettePrevious);
        Add(bindings, [KeyMode.Command], PaletteView, "Down", ActionIds.PaletteNext);
        Add(bindings, [KeyMode.Command], PaletteView, "Ctrl-j", ActionIds.PaletteNext);
        Add(bindings, [KeyMode.Command], PaletteView, "Ctrl-k", ActionIds.PalettePrevious);
        Add(bindings, [KeyMode.Command], PaletteView, "Ctrl-l", ActionIds.CommandExecute);
    }

    private static void AddContextMenu(List<KeyBinding> bindings)
    {
        foreach (var (keys, action) in new[]
        {
            ("j", ActionIds.PaletteNext),
            ("Down", ActionIds.PaletteNext),
            ("Ctrl-j", ActionIds.PaletteNext),
            ("k", ActionIds.PalettePrevious),
            ("Up", ActionIds.PalettePrevious),
            ("Ctrl-k", ActionIds.PalettePrevious),
            ("Enter", ActionIds.CommandExecute),
            ("Ctrl-l", ActionIds.CommandExecute),
        })
        {
            Add(bindings, NormalOnly, ContextMenuView, keys, action);
        }
    }

    private static void AddModals(List<KeyBinding> bindings)
    {
        const string global = KeyBindingTable.GlobalView;
        Add(bindings, [KeyMode.Insert], global, "Enter", ActionIds.InputAccept);
        Add(bindings, [KeyMode.Command], global, "Enter", ActionIds.CommandExecute);
        Add(bindings, [KeyMode.Confirm], global, "y", ActionIds.ConfirmYes);
        Add(bindings, [KeyMode.Confirm], global, "n", ActionIds.ConfirmNo);
    }

    private static void Add(List<KeyBinding> bindings, KeyMode[] modes, string view, string keys, ActionId action)
    {
        var sequence = KeySequenceParser.Parse(keys);
        foreach (var mode in modes)
        {
            bindings.Add(new KeyBinding(mode, view, sequence, action));
        }
    }
}
