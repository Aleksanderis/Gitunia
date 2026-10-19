namespace Gitunia.Core.FileTree;

/// <summary>One visible row of the file tree of a commit.</summary>
/// <param name="Kind">Directory, file, executable, symbolic link or submodule.</param>
/// <param name="Path">Full path from the repository root; for a folded directory the deepest one. It is the key of the
/// expanded set.</param>
/// <param name="Label">What the row shows: the name, or <c>a/b/c</c> for a folded directory chain.</param>
/// <param name="Depth">Nesting level, 0 at the top.</param>
/// <param name="IsExpanded">A directory whose children are listed below it.</param>
/// <param name="Size">Size of a file in bytes, null for directories and submodules.</param>
/// <param name="FileCount">Number of files below a directory (all of them, also while it is collapsed); 0 for other
/// rows.</param>
public sealed record FileTreeRow(
    FileTreeRowKind Kind,
    string Path,
    string Label,
    int Depth,
    bool IsExpanded,
    long? Size,
    int FileCount);
