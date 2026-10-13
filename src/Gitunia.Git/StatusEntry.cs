namespace Gitunia.Git;

/// <summary>One path in <c>git status</c>.</summary>
/// <param name="Kind">Which record type the entry came from.</param>
/// <param name="Path">Path relative to the repository root, exactly as git reported it.</param>
/// <param name="OriginalPath">Source path of a rename or copy; null otherwise.</param>
/// <param name="IndexChange">Change between HEAD and the index (staged); unmodified for untracked and ignored entries.</param>
/// <param name="WorktreeChange">Change between the index and the working tree (unstaged); unmodified for untracked and ignored entries.</param>
/// <param name="Similarity">Rename or copy similarity in percent; null otherwise.</param>
/// <param name="Conflict">Kind of conflict for unmerged entries, otherwise <see cref="ConflictKind.None"/>.</param>
/// <param name="Submodule">Submodule state, or null when the path is not a submodule.</param>
public sealed record StatusEntry(
    StatusEntryKind Kind,
    string Path,
    string? OriginalPath,
    FileChangeKind IndexChange,
    FileChangeKind WorktreeChange,
    int? Similarity,
    ConflictKind Conflict,
    SubmoduleState? Submodule)
{
    /// <summary>The path has a change recorded in the index.</summary>
    public bool IsStaged => Kind is StatusEntryKind.Ordinary or StatusEntryKind.Renamed or StatusEntryKind.Copied
        && IndexChange != FileChangeKind.Unmodified;

    /// <summary>The path has a change in the working tree that is not staged.</summary>
    public bool IsUnstaged => Kind is StatusEntryKind.Ordinary or StatusEntryKind.Renamed or StatusEntryKind.Copied
        && WorktreeChange != FileChangeKind.Unmodified;
}
