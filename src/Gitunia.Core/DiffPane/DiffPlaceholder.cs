namespace Gitunia.Core.DiffPane;

/// <summary>The note the Files tab's pane beside the tree shows while no file is under the tree's cursor.</summary>
public enum DiffPlaceholder
{
    /// <summary>No note: the pane shows a diff or a file's content.</summary>
    None,

    /// <summary>The cursor is on a directory or nothing.</summary>
    File,

    /// <summary>The cursor is on a submodule.</summary>
    Submodule,
}
