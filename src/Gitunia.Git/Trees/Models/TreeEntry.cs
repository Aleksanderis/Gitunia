namespace Gitunia.Git.Trees.Models;

/// <summary>One path of the tree of a commit, as <c>git ls-tree -r --long</c> lists it (directories are implied by the
/// paths).</summary>
/// <param name="Path">Path from the repository root, with <c>/</c> separators.</param>
/// <param name="Kind">File, executable, symbolic link or submodule.</param>
/// <param name="Id">The object the path points at (a commit for a submodule).</param>
/// <param name="Size">Size of the blob in bytes, null for a submodule.</param>
public sealed record TreeEntry(string Path, TreeEntryKind Kind, ObjectId Id, long? Size);
