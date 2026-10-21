namespace Gitunia.Core.Workspaces.Tabs;

/// <summary>How a file list shows the path of a file: the Changes tab of the detail pane and the working-tree
/// Unstaged and Staged lists.</summary>
public enum FilePathStyle
{
    /// <summary>The file name first, then the directory dimmed; the directory is cut from the left when the row is
    /// narrow.</summary>
    NameFirst,

    /// <summary>The full path from the repository root, the name last.</summary>
    FullPath,
}
