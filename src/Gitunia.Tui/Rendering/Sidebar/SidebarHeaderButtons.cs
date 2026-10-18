using Gitunia.Core.Mouse;
using Gitunia.Core.Sidebar;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>
/// The buttons at the right end of a sidebar section header: the mouse path of the section's actions. A section lists
/// its buttons in <see cref="Specifications"/> (most important first); the row keeps as many as fit after the header
/// text and drops the rest from the end. A button acts on what the keys act on: the selected row, so a click on a row
/// selects it first. Other sections add their rows here (tags).
/// </summary>
public static class SidebarHeaderButtons
{
    /// <summary>One button: the section whose header carries it, the mouse-map view of its click, the label.</summary>
    public sealed record Specification(SidebarSection Section, string View, string Label);

    /// <summary>Every header button, in the order they appear left to right.</summary>
    public static IReadOnlyList<Specification> Specifications { get; } =
    [
        new(SidebarSection.Branches, DefaultMouseMap.SidebarBranchNewButton, "[new]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarBranchSwitchButton, "[switch]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarBranchRenameButton, "[rename]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarBranchDeleteButton, "[delete]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarBranchNewOnlyButton, "[new-only]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarRebaseButton, "[rebase]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarResetButton, "[reset]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarPickButton, "[pick]"),
        new(SidebarSection.Branches, DefaultMouseMap.SidebarRevertButton, "[revert]"),
        new(SidebarSection.Tags, DefaultMouseMap.SidebarRebaseButton, "[rebase]"),
        new(SidebarSection.Tags, DefaultMouseMap.SidebarResetButton, "[reset]"),
        new(SidebarSection.Tags, DefaultMouseMap.SidebarPickButton, "[pick]"),
        new(SidebarSection.Tags, DefaultMouseMap.SidebarRevertButton, "[revert]"),
        new(SidebarSection.Worktrees, DefaultMouseMap.SidebarWorktreeAddButton, "[add]"),
        new(SidebarSection.Worktrees, DefaultMouseMap.SidebarWorktreeRemoveButton, "[remove]"),
        new(SidebarSection.Worktrees, DefaultMouseMap.SidebarWorktreeLockButton, "[lock]"),
        new(SidebarSection.Worktrees, DefaultMouseMap.SidebarWorktreeUnlockButton, "[unlock]"),
        new(SidebarSection.Worktrees, DefaultMouseMap.SidebarWorktreePruneButton, "[prune]"),
        new(SidebarSection.Stashes, DefaultMouseMap.SidebarStashPushButton, "[push]"),
        new(SidebarSection.Stashes, DefaultMouseMap.SidebarStashApplyButton, "[apply]"),
        new(SidebarSection.Stashes, DefaultMouseMap.SidebarStashPopButton, "[pop]"),
        new(SidebarSection.Stashes, DefaultMouseMap.SidebarStashDropButton, "[drop]"),
        new(SidebarSection.Stashes, DefaultMouseMap.SidebarStashPushUntrackedButton, "[push-all]"),
    ];

    /// <summary>
    /// The buttons of the header <paramref name="row"/> in a line of <paramref name="width"/> columns (start columns
    /// counted from the first column), or none for any other row. They sit flush right, one blank column apart, and
    /// start at least one column after <paramref name="headerWidth"/>.
    /// </summary>
    public static IReadOnlyList<TitleButton> Layout(SidebarRow row, int headerWidth, int width)
    {
        if (!row.IsSection)
        {
            return [];
        }

        var chosen = new List<Specification>();
        var total = 0;
        foreach (var specification in Specifications.Where(candidate => candidate.Section == row.Section))
        {
            var next = total + (chosen.Count > 0 ? 1 : 0) + TextWidth.Of(specification.Label);
            if (headerWidth + 1 + next > width)
            {
                break;
            }

            chosen.Add(specification);
            total = next;
        }

        var buttons = new List<TitleButton>(chosen.Count);
        var column = width - total;
        foreach (var specification in chosen)
        {
            buttons.Add(new TitleButton(specification.View, specification.Label, column));
            column += TextWidth.Of(specification.Label) + 1;
        }

        return buttons;
    }
}
