using Gitunia.Core.FileTree;
using Gitunia.Git;

namespace Gitunia.Core.DiffPane;

/// <summary>What the Files tab's pane beside the tree should show for the row under the tree's cursor.</summary>
/// <param name="File">The file row whose content to show, null for the placeholder.</param>
/// <param name="Commit">The commit the file is read from.</param>
/// <param name="Placeholder">The note to show when there is no file.</param>
public sealed record DiffContentTarget(FileTreeRow? File, ObjectId? Commit, DiffPlaceholder Placeholder)
{
    /// <summary>No file under the cursor.</summary>
    public static DiffContentTarget None { get; } = new(null, null, DiffPlaceholder.File);

    /// <summary>A file's content is shown only while the commit its tree was read for is the one selected in the graph.</summary>
    /// <param name="row">The tree row under the cursor, null for none.</param>
    /// <param name="treeCommit">The commit the tree shows.</param>
    /// <param name="graphCommit">The commit selected in the graph.</param>
    public static DiffContentTarget Of(FileTreeRow? row, ObjectId? treeCommit, ObjectId? graphCommit)
    {
        if (row is { Kind: not (FileTreeRowKind.Directory or FileTreeRowKind.Submodule), Blob: not null }
            && treeCommit is { } shown
            && graphCommit == shown)
        {
            return new DiffContentTarget(row, shown, DiffPlaceholder.None);
        }

        var placeholder = row?.Kind == FileTreeRowKind.Submodule ? DiffPlaceholder.Submodule : DiffPlaceholder.File;

        return new DiffContentTarget(null, null, placeholder);
    }

    /// <summary>The pane already shows this target.</summary>
    /// <param name="state">The pane state.</param>
    public bool IsShownBy(DiffPaneState state) => File is { } row
        ? state.ContentPath == row.Path && state.ContentCommit == Commit
        : state.ContentPath is null && state.Placeholder == Placeholder;
}
