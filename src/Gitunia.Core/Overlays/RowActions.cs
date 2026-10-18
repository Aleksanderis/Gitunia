using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The actions a right click on a row offers (#428): the command popup is the palette filtered to these, in this
/// order. They are the actions that take the selected row as their target, so the click selects the row first.
/// </summary>
public static class RowActions
{
    /// <summary>The actions on a branch or tag row of the sidebar.</summary>
    public static IReadOnlyList<ActionId> OfSidebarRow { get; } =
    [
        ActionIds.BranchSwitch,
        ActionIds.BranchCreate,
        ActionIds.BranchRename,
        ActionIds.BranchDelete,
        ActionIds.TagCreate,
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
        ActionIds.MergeBranch,
        ActionIds.MergeBranchNoFastForward,
        ActionIds.MergeBranchSquash,
        ActionIds.RebaseOnto,
        ActionIds.ResetBranch,
        ActionIds.PickCherry,
        ActionIds.PickRevert,
        ActionIds.GraphCopyHash,
    ];
}
