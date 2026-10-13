namespace Gitunia.Git;

/// <summary>One file a commit changed, relative to its first parent (or to the empty tree for a root commit).</summary>
/// <param name="Path">Path after the change; for a deleted file the path it had.</param>
/// <param name="OldPath">Path before a rename or copy, null otherwise.</param>
/// <param name="Kind">Added, modified, deleted, renamed, copied or type-changed.</param>
/// <param name="Similarity">Similarity percentage of a rename or copy, null otherwise.</param>
public sealed record CommitFileChange(string Path, string? OldPath, FileChangeKind Kind, int? Similarity);
