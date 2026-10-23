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
        ActionIds.RebaseInteractive,
        ActionIds.ResetBranch,
        ActionIds.PickCherry,
        ActionIds.PickRevert,
    ];

    /// <summary>The title of the dropdown of the diff's view options (#645).</summary>
    public const string ViewOptionsTitle = "View";

    /// <summary>The view options the diff title's <c>view ▾</c> dropdown lists (#645), in this order.</summary>
    public static IReadOnlyList<ActionId> ViewOptions { get; } =
    [
        ActionIds.DiffMoreContext,
        ActionIds.DiffLessContext,
        ActionIds.DiffToggleWholeFile,
        ActionIds.DiffToggleIgnoreWhitespace,
        ActionIds.DiffToggleSideBySide,
        ActionIds.DiffToggleWrap,
    ];

    /// <summary>
    /// The actions on the commit of a graph row. Open details is the mouse route to the details pane, which a
    /// double-click no longer reaches and which is hidden on screens under 14 rows (#486).
    /// </summary>
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
        ActionIds.RebaseFixup,
        ActionIds.RebaseReword,
        ActionIds.RebaseDrop,
        ActionIds.GraphCopyHash,
        ActionIds.GraphOpenDetails,
        ActionIds.GraphCompare,
    ];

    /// <summary>The graph row actions; Open details is left out while the details pane is hidden (#490).</summary>
    public static IReadOnlyList<ActionId> OfGraphRowFor(bool detailsVisible) => OfGraphRowFor(detailsVisible, false);

    /// <summary>
    /// The graph row actions. The root commit (no parents) also offers the interactive rebase of the whole history
    /// (#561), placed after the rebase onto the commit.
    /// </summary>
    public static IReadOnlyList<ActionId> OfGraphRowFor(bool detailsVisible, bool isRootCommit)
    {
        List<ActionId> actions =
            [.. OfGraphRow.Where(action => detailsVisible || action != ActionIds.GraphOpenDetails)];
        if (isRootCommit)
        {
            actions.Insert(actions.IndexOf(ActionIds.RebaseInteractive) + 1, ActionIds.RebaseInteractiveRoot);
        }

        return actions;
    }

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
            ActionIds.BranchFastForward,
            ActionIds.WorktreeOpen,
            .. Integrate,
        ],
        SidebarRowKind.RemoteBranch =>
        [
            ActionIds.BranchSwitch,
            ActionIds.BranchCreate,
            ActionIds.BranchCreateOnly,
            ActionIds.RemoteDeleteBranch,
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
        SidebarSection.Remotes =>
        [
            ActionIds.RemoteFetch,
            ActionIds.RemoteFetchAll,
            ActionIds.RemotePull,
            ActionIds.RemotePush,
            ActionIds.RemotePushForce,
            ActionIds.RemoteDeleteBranch,
            ActionIds.RemoteAdd,
            ActionIds.RemoteRemove,
            ActionIds.RemoteRename,
            ActionIds.RemoteSetUrl,
        ],
        SidebarSection.Tags => [ActionIds.TagCreate],
        SidebarSection.Worktrees => [ActionIds.WorktreeAdd, ActionIds.WorktreePrune],
        SidebarSection.Stashes => [ActionIds.StashPush, ActionIds.StashPushUntracked],
        _ => [],
    };
}
