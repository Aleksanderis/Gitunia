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

    /// <summary>The Changes tab lists the files as a tree of folders, each file under its folder with its name only.
    /// The working-tree lists have no tree and show their rows name first.</summary>
    Tree,
}
