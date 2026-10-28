using Gitunia.Core.Sidebar;

namespace Gitunia.Tui.App.Branches;

/// <summary>What a branch action acts on: the selected sidebar row, or the commit selected in the graph.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when the graph is the target.</param>
/// <param name="Name">The branch or tag name of the sidebar row, null when the graph is the target.</param>
/// <param name="CommitHash">The commit of the row or of the graph selection, null when there is none.</param>
public sealed record BranchSelection(SidebarRowKind? RowKind, string? Name, string? CommitHash)
{
    /// <summary>
    /// The selection for a focused sidebar row (null when another pane has focus, then the graph commit is used).
    /// A leaf row names itself, a folder of the Branches section its path, and any other section or folder row
    /// (Remotes, Tags, ...) is a nameless Section selection and never falls back to the graph commit.
    /// </summary>
    public static BranchSelection Of(SidebarRow? sidebarRow, Func<string?> graphCommit) => sidebarRow switch
    {
        { IsContainer: false } row => new BranchSelection(row.Kind, row.Label, row.CommitHash),
        { Kind: SidebarRowKind.Folder, Section: SidebarSection.Branches } folder =>
            new BranchSelection(SidebarRowKind.Folder, SidebarFolders.PathOf(folder), null),
        { } => new BranchSelection(SidebarRowKind.Section, null, null),
        null => new BranchSelection(null, null, graphCommit()),
    };
}
