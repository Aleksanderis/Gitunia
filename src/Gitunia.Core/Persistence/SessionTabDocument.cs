namespace Gitunia.Core.Persistence;

/// <summary>One open tab as stored in the session file. Every field has a default so older or hand-edited files still load.</summary>
public sealed record SessionTabDocument
{
    /// <summary>The working tree root, null for a bare repository.</summary>
    public string? WorkingTreePath { get; init; }

    /// <summary>The git directory of the tab's working tree.</summary>
    public string? GitDirectory { get; init; }

    /// <summary>The git directory shared by all worktrees.</summary>
    public string? CommonDirectory { get; init; }

    /// <summary>Whether the repository is bare.</summary>
    public bool IsBare { get; init; }

    /// <summary>Whether the tab shows a linked worktree.</summary>
    public bool IsLinkedWorktree { get; init; }

    /// <summary>Hex id of the selected commit, null to select the newest.</summary>
    public string? SelectedCommit { get; init; }

    /// <summary>Index of the first visible graph row.</summary>
    public int ScrollOffset { get; init; }

    /// <summary>The active graph filter.</summary>
    public string? FilterText { get; init; }

    /// <summary>Name of the focused pane.</summary>
    public string? FocusedPane { get; init; }

    /// <summary>Sidebar columns the user set; null (or absent in older files) is automatic.</summary>
    public int? SidebarWidth { get; init; }

    /// <summary>Bottom strip rows the user set; null is automatic.</summary>
    public int? BottomHeight { get; init; }

    /// <summary>Detail pane columns beside the diff the user set; null is automatic.</summary>
    public int? DetailWidth { get; init; }

    /// <summary>Name of the bottom detail pane tab (<c>Details</c>, <c>Changes</c>, <c>Files</c>); null or unknown (older files) is <c>Changes</c>.</summary>
    public string? BottomTab { get; init; }
}
