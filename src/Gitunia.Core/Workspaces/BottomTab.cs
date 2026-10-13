namespace Gitunia.Core.Workspaces;

/// <summary>The tabs of the bottom detail pane, in the order of the tab strip.</summary>
public enum BottomTab
{
    /// <summary>Hash, author, committer, parents, refs and the full message.</summary>
    Details,

    /// <summary>The changed files with their status.</summary>
    Changes,

    /// <summary>The file tree of the commit (a placeholder for now).</summary>
    Files,
}
