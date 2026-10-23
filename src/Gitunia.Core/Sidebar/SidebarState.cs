using System.Collections.Immutable;

namespace Gitunia.Core.Sidebar;

/// <summary>How a tab's sidebar looks: which sections are folded, the name filter and the cursor row.</summary>
/// <param name="CollapsedSections">Sections whose entries are hidden.</param>
/// <param name="FilterText">Case-insensitive name filter, empty when none.</param>
/// <param name="Cursor">Index of the selected row in the visible rows; clamped when the rows change.</param>
/// <param name="OpenedFolders">Folders the user opened (<see cref="SidebarRow.Key"/>); per tab, not persisted.</param>
/// <param name="ClosedFolders">
/// Folders the user closed. A folder in neither list is open when it holds the current branch or is a remote's own
/// folder, and closed otherwise.
/// </param>
public sealed record SidebarState(
    ImmutableArray<SidebarSection> CollapsedSections,
    string FilterText,
    int Cursor,
    ImmutableArray<string> OpenedFolders,
    ImmutableArray<string> ClosedFolders)
{
    /// <summary>Branches, worktrees and stashes open; the long remote and tag lists folded; cursor on the first row.</summary>
    public static SidebarState Initial { get; } =
        new([SidebarSection.Remotes, SidebarSection.Tags], string.Empty, 0, [], []);

    /// <summary>Whether a filter is active.</summary>
    public bool HasFilter => FilterText.Length > 0;
}
