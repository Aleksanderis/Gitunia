using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The actions the menu of a row offers (#428, #471), in this order. They take the selected row as their target,
/// so opening the menu selects the row first. Together they cover everything the sidebar header chips offered, so the
/// menu is the mouse path of an action and its keys are shown beside it.
/// </summary>
public static class RowActions
{
    private static readonly ActionId[] Integrate =
    [
        ActionIds.MergeBranch,
        ActionIds.MergeBranchNoFastForward,
        ActionIds.MergeBranchSquash,
        ActionIds.RebaseOnto,
        ActionIds.ResetBranch,
        ActionIds.PickCherry,
        ActionIds.PickRevert,
    ];

    /// <summary>The actions on the commit of a graph row.</summary>
    public static IReadOnlyList<ActionId> OfGraphRow { get; } =
    [
        ActionIds.BranchCreate,
        ActionIds.BranchCreateOnly,
        ActionIds.BranchSwitch,
        ActionIds.TagCreate,
        ActionIds.TagDelete,
        ActionIds.TagPush,
        ActionIds.TagDeleteRemote,
        .. Integrate,
        ActionIds.GraphCopyHash,
    ];

    /// <summary>The actions on a sidebar row: a section header, a branch, a tag, a worktree or a stash.</summary>
    public static IReadOnlyList<ActionId> OfSidebarRow(SidebarRow row) => row.Kind switch
    {
        SidebarRowKind.Section => OfSection(row.Section),
        SidebarRowKind.LocalBranch =>
        [
            ActionIds.BranchSwitch,
            ActionIds.BranchCreate,
            ActionIds.BranchCreateOnly,
            ActionIds.BranchRename,
            ActionIds.BranchDelete,
            ActionIds.BranchSetUpstream,
            ActionIds.BranchUnsetUpstream,
            .. Integrate,
        ],
        SidebarRowKind.RemoteBranch =>
        [
            ActionIds.BranchSwitch,
            ActionIds.BranchCreate,
            ActionIds.BranchCreateOnly,
            .. Integrate,
        ],
        SidebarRowKind.Tag =>
        [
            ActionIds.TagCreate,
            ActionIds.TagDelete,
            ActionIds.TagPush,
            ActionIds.TagDeleteRemote,
            ActionIds.BranchCreate,
            ActionIds.BranchCreateOnly,
            .. Integrate,
        ],
        SidebarRowKind.Worktree =>
        [
            ActionIds.WorktreeSwitch,
            ActionIds.WorktreeOpen,
            ActionIds.WorktreeAdd,
            ActionIds.WorktreeRemove,
            ActionIds.WorktreeLock,
            ActionIds.WorktreeUnlock,
            ActionIds.WorktreePrune,
        ],
        SidebarRowKind.Stash =>
        [
            ActionIds.StashApply,
            ActionIds.StashPop,
            ActionIds.StashDrop,
            ActionIds.StashPush,
            ActionIds.StashPushUntracked,
        ],
        _ => [],
    };

    private static ActionId[] OfSection(SidebarSection section) => section switch
    {
        SidebarSection.Branches => [ActionIds.BranchCreate, ActionIds.BranchCreateOnly],
        SidebarSection.Tags => [ActionIds.TagCreate],
        SidebarSection.Worktrees => [ActionIds.WorktreeAdd, ActionIds.WorktreePrune],
        SidebarSection.Stashes => [ActionIds.StashPush, ActionIds.StashPushUntracked],
        _ => [],
    };
}
